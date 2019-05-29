/*
 * Copyright (c) 2019 Dummiesman
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
*/

using Dummiesman;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MTLLoader {
    public List<string> SearchPaths = new List<string>() { "%FileName%_Textures", string.Empty};

    private FileInfo _objFileInfo = null;

    /// <summary>
    /// The texture loading function. Overridable for stream loading purposes.
    /// </summary>
    /// <param name="path">The path supplied by the OBJ file, converted to OS path seperation</param>
    /// <param name="isNormalMap">Whether the loader is requesting we convert this into a normal map</param>
    /// <returns>Texture2D if found, or NULL if missing</returns>
    public virtual Texture2D TextureLoadFunction(string path, bool isNormalMap)
    {
        //find it
        foreach (var searchPath in SearchPaths)
        {
            //replace varaibles and combine path
            string processedPath = (_objFileInfo != null) ? searchPath.Replace("%FileName%", Path.GetFileNameWithoutExtension(_objFileInfo.Name)) 
                                                          : searchPath;
            string filePath = Path.Combine(processedPath, path);

            //return if eists
            if (File.Exists(filePath))
            {
                var tex = ImageLoader.LoadTexture(filePath);

                if(isNormalMap)
                    ImageUtils.ConvertToNormalMap(tex);

                return tex;
            }
        }

        //not found
        return null;
    }

    private Texture2D TryLoadTexture(string texturePath, bool normalMap = false)
    {
        //swap directory seperator char
        texturePath = texturePath.Replace('\\', Path.DirectorySeparatorChar);
        texturePath = texturePath.Replace('/', Path.DirectorySeparatorChar);

        return TextureLoadFunction(texturePath, normalMap);
    }

    /// <summary>
    /// Loads a *.mtl file
    /// </summary>
    /// <param name="input">The input stream from the MTL file</param>
    /// <returns>Dictionary containing loaded materials</returns>
    public Dictionary<string, Material> Load(Stream input)
    {
        var inputReader = new StreamReader(input);
        var reader = new StringReader(inputReader.ReadToEnd());

        Dictionary<string, Material> mtlDict = new Dictionary<string, Material>();
        Material currentMaterial = null;

        for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string processedLine = line.Clean();
            string[] splitLine = processedLine.Split(' ');

            //blank or comment
            if (splitLine.Length < 2 || processedLine[0] == '#')
                continue;

            //newmtl
            if (splitLine[0] == "newmtl")
            {
                string materialName = processedLine.Substring(7);

                var newMtl = new Material(Shader.Find("Standard (Specular setup)")) { name = materialName };
                mtlDict[materialName] = newMtl;
                currentMaterial = newMtl;

                continue;
            }

            //anything past here requires a material instance
            if (currentMaterial == null)
                continue;

            //diffuse color
            if (splitLine[0] == "Kd" || splitLine[0] == "kd")
            {
                currentMaterial.SetColor("_Color", OBJLoaderHelper.ColorFromStrArray(splitLine));
                continue;
            }

            //diffuse map
            if (splitLine[0] == "map_Kd" || splitLine[0] == "map_kd")
            {
                string texturePath = processedLine.Substring(7);

                var KdTexture = TryLoadTexture(texturePath);
                currentMaterial.SetTexture("_MainTex", KdTexture);

                //set transparent mode if the texture has transparency
                if(KdTexture != null && (KdTexture.format == TextureFormat.DXT5 || KdTexture.format == TextureFormat.ARGB32))
                {
                    OBJLoaderHelper.EnableMaterialTransparency(currentMaterial);
                }

                //flip texture if this is a dds
                if(Path.GetExtension(texturePath).ToLower() == ".dds")
                {
                    currentMaterial.mainTextureScale = new Vector2(1f, -1f);
                }

                continue;
            }

            //bump map
            if (splitLine[0] == "map_Bump" || splitLine[0] == "map_bump")
            {
                string texturePath = processedLine.Substring(9);
                var bumpTexture = TryLoadTexture(texturePath, true);

                if (bumpTexture != null) {
                    currentMaterial.SetTexture("_BumpMap", bumpTexture);
                    currentMaterial.EnableKeyword("_NORMALMAP");
                }

                continue;
            }

            //specular color
            if (splitLine[0] == "Ks" || splitLine[0] == "ks")
            {
                currentMaterial.SetColor("_SpecColor", OBJLoaderHelper.ColorFromStrArray(splitLine));
                continue;
            }

            //emission color
            if (splitLine[0] == "Ka" || splitLine[0] == "ka")
            {
                currentMaterial.SetColor("_EmissionColor", OBJLoaderHelper.ColorFromStrArray(splitLine, 0.05f));
                currentMaterial.EnableKeyword("_EMISSION");
                continue;
            }

            //emission map
            if (splitLine[0] == "map_Ka" || splitLine[0] == "map_ka")
            {
                string texturePath = processedLine.Substring(9);
                currentMaterial.SetTexture("_EmissionMap", TryLoadTexture(texturePath));
                continue;
            }

            //alpha
            if (splitLine[0] == "d")
            {
                float visibility = OBJLoaderHelper.FastFloatParse(splitLine[1]);
                if(visibility < (1f - Mathf.Epsilon))
                {
                    Color temp = currentMaterial.color;

                    temp.a = visibility;
                    currentMaterial.SetColor("_Color", temp);

                    OBJLoaderHelper.EnableMaterialTransparency(currentMaterial);
                }
                continue;
            }

            //glossiness
            if (splitLine[0] == "Ns" || splitLine[0] == "ns")
            {
                float Ns = OBJLoaderHelper.FastFloatParse(splitLine[1]);
                Ns = (Ns / 1000f);
                currentMaterial.SetFloat("_Glossiness", Ns);
            }
        }

        //return our dict
        return mtlDict;
    }

    /// <summary>
    /// Loads a *.mtl file
    /// </summary>
    /// <param name="path">The path to the MTL file</param>
    /// <returns>Dictionary containing loaded materials</returns>
	public Dictionary<string, Material> Load(string path)
    {
        _objFileInfo = new FileInfo(path); //get file info
        SearchPaths.Add(_objFileInfo.Directory.FullName); //add root path to search dir

        using (var fs = new FileStream(path, FileMode.Open))
        {
            return Load(fs); //actually load
        }
        
    }
}
