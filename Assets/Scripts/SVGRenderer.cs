using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class SVGRenderer : MonoBehaviour
{

    #region Private variables
    private Camera camA = null;
    private GameObject camAGO = null;

    private uint[] initHead;

    private int Width { get { return GetComponent<Camera>().pixelWidth; } }
    private int Height { get { return GetComponent<Camera>().pixelHeight; } }

    private float pixelStrideZCutoff = 0.0f;
    private float pixelZSizeOffset = 0.0f;

    private int maxIntIteration = 0;

    private Camera _camera = null;

    private List<List<Contour>> idContoursAllLayers;
    private List<List<Contour>> colorContoursAllLayers;

    private bool runOnceId;
    private bool runOnceFinal;

    private List<Transform> allMeshes; // Not parents!!
    //private GameObject loadedObject;
    private Transform loadedMesh;

    Dictionary<string, List<Color>> materialMap;

    #endregion

    #region Public variables
    [Header("Downsample:")]
    [Range(1.0f, 36.0f)]
    public int perPixelLinkedListDepth = 1;

    [Header("Public variables:")]
    public bool Refinement = true;
    [Range(0.0f, 50.0f)]
    public int pixelStride = 1;

    [Header("Vector Graphics Variables:")]
    public String SVGFilePath;
    [Range(1, 30)]
    public int NumberOfLayers;
    [Range(1, 10)]
    public int SilhouetteMergingThreshold;
    [Range(0, 10)]
    public int SilhouetteMergingIterations;
    public bool SortLayersByDepth = true;

    public ComputeBuffer nodeBuffer;
    public ComputeBuffer head;
    public ComputeBuffer counter;
    #endregion

    private void Awake()
    {

        _camera = GetComponent<Camera>();
        _camera.renderingPath = RenderingPath.DeferredShading;
        //_camera.opaqueSortMode = OpaqueSortMode.FrontToBack;

        //_camera.rect = new Rect(0, 0, Width, Height);

        //_camera.depthTextureMode |= DepthTextureMode.Depth;
        if (camAGO != null)
        {
            DestroyImmediate(camAGO);
        }
        camAGO = new GameObject("Per-Pixel-Linked-List-Cam")
        {
            hideFlags = HideFlags.DontSave
        };

        camAGO.transform.parent = transform;
        camAGO.transform.localPosition = Vector3.zero;

        camA = camAGO.AddComponent<Camera>();
        camA.CopyFrom(_camera);
        camA.clearFlags = CameraClearFlags.SolidColor;

        //camA.opaqueSortMode = OpaqueSortMode.FrontToBack;

        camA.enabled = false;

        initHead = new uint[Height * Width];
        for (int i = 0; i < initHead.Length; i++)
        {
            initHead[i] = 0xffffffff;
        }

        maxIntIteration = (int)(Mathf.Sqrt(Width * Width + Height * Height));

        runOnceId = false;
        runOnceFinal = false;
    }

    private void OnDestroy()
    {
        DestroyImmediate(camAGO);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        #region Disable textures
        // Finne mesher
        allMeshes = new List<Transform>();
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.GetComponent<Transform>().childCount > 0)
            {
                if (go.GetComponent<Transform>().GetChild(0).GetComponent<Renderer>() != null)
                {
                    for (int i = 0; i < go.GetComponent<Transform>().childCount; i++)
                    {
                        allMeshes.Add(go.GetComponent<Transform>().GetChild(i));
                    }
                }
            }
        }

        // Fjerne teksturene
        for (int i = 0; i < allMeshes.Count; i++)
        {
            Material[] materials = allMeshes[i].GetComponent<Renderer>().materials;
            for (int j = 0; j < materials.Length; j++)
            {
                allMeshes[i].GetComponent<Renderer>().materials[j].mainTexture = null;
            }
        }
        #endregion

        #region Assigning group colors to meshes
        System.Random random = new System.Random();
        materialMap = new Dictionary<string, List<Color>>();

        // Give every triangle in a mesh a single, random color
        for (int i = 0; i < allMeshes.Count; i++)
        {
            List<Color> colorList = new List<Color>();
            Color newColor = VectorUtils.RandomRGB(random);

            Material[] materials = allMeshes[i].GetComponent<Renderer>().materials;
            String meshName = allMeshes[i].name;
            for (int j = 0; j < materials.Length; j++)
            {
                Color oldColor = materials[j].color; // Original color
                colorList.Add(oldColor);

                allMeshes[i].GetComponent<Renderer>().materials[j].color = newColor; // Temporary ID color 
            }
            materialMap.Add(meshName, colorList);
        }

        #endregion

        #region Per-Pixel Linked list

        GeneratePerPixelLinkedList();

        #endregion

        #region ID rendering

        camA.Render();

        if (!runOnceId)
        {
            idContoursAllLayers = new List<List<Contour>>();

            int i = 0;
            while (i < NumberOfLayers) // *4
            {
                Shader.SetGlobalInt("_loopMax", i);

                if(SortLayersByDepth)
                {
                    Shader.SetGlobalFloat("_sortLayers", 1);
                } else
                {
                    Shader.SetGlobalFloat("_sortLayers", 0);
                }

                Graphics.ClearRandomWriteTargets();
                Graphics.SetRandomWriteTarget(1, nodeBuffer);
                //Graphics.SetRandomWriteTarget(2, head);

                RenderTexture renderTex = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.Default);
                Material mat = new Material(Shader.Find("Hidden/VectorShader"));

                Graphics.Blit(source, renderTex, mat);
                Graphics.Blit(renderTex, destination);

                // Copy from RenderTexture to a texture
                RenderTexture.active = renderTex;
                Texture2D tex2d = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex2d.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                tex2d.Apply();

                Rect rect = new Rect(0, 0, Width, Height);

                List<Contour> idContoursCurrentLayer = ExtractLayerContours(tex2d); // EXTRACTING CONTOURS
                idContoursAllLayers.Add(idContoursCurrentLayer);

                RenderTexture.ReleaseTemporary(renderTex);

                //VectorUtils.SaveTextureToFile(tex2d, "idldiTex" + i + ".png");

                i += 1; // += 4
            }
            //Graphics.ClearRandomWriteTargets(); // meg

            // Storing textures for viewing
            /*for (int j = 0; j < idContoursAllLayers.Count; j++)
            {
                if (idContoursAllLayers[j].Count != 0)
                {
                    Texture2D contourTexture = VectorUtils.ContourTexture(idContoursAllLayers[j], Width, Height);
                    VectorUtils.SaveTextureToFile(contourTexture, "idTex" + j + ".png");
                }
            }*/

            runOnceId = true;
        }
        #endregion

        #region Reset mesh colors

        // Reset all mesh colors
        for (int i = 0; i < allMeshes.Count; i++)
        {
            Material[] materials = allMeshes[i].GetComponent<Renderer>().materials;
            String meshName = allMeshes[i].name;
            List<Color> colorList = materialMap[meshName];
            for (int j = 0; j < materials.Length; j++)
            {
                allMeshes[i].GetComponent<Renderer>().materials[j].color = colorList[j]; // NB: sharedMaterials ruins everything!!
            }
        }

        #endregion

        #region Per-Pixel Linked list 2

        ReleaseBuffers();

        GeneratePerPixelLinkedList();

        #endregion

        #region Rendering final scene

        camA.Render();

        if (!runOnceFinal)
        {
            colorContoursAllLayers = new List<List<Contour>>();

            int i = 0;
            while (i < NumberOfLayers) //*4
            {
                Shader.SetGlobalInt("_loopMax", i*4);

                if (SortLayersByDepth)
                {
                    Shader.SetGlobalFloat("_sortLayers", 1);
                }
                else
                {
                    Shader.SetGlobalFloat("_sortLayers", 0);
                }

                Graphics.ClearRandomWriteTargets();
                Graphics.SetRandomWriteTarget(1, nodeBuffer);
                //Graphics.SetRandomWriteTarget(2, head);

                RenderTexture renderTex = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.Default);
                Material mat = new Material(Shader.Find("Hidden/VectorShader"));

                Graphics.Blit(source, renderTex, mat);
                Graphics.Blit(renderTex, destination);

                // Copy from RenderTexture to a texture
                RenderTexture.active = renderTex;
                Texture2D tex2d = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex2d.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                tex2d.Apply();

                Rect rect = new Rect(0, 0, Width, Height);

                List<Contour> colorContoursCurrentLayer = ExtractLayerContours(tex2d);

                colorContoursAllLayers.Add(colorContoursCurrentLayer);

                RenderTexture.ReleaseTemporary(renderTex);

                //VectorUtils.SaveTextureToFile(tex2d, "ldiTex" + i + ".png");

                i += 1;
            }
            //Graphics.ClearRandomWriteTargets(); // meg

            // Storing textures for viewing
            /*for (int j = 0; j < colorContoursAllLayers.Count; j++)
            {
                if (colorContoursAllLayers[j].Count != 0)
                {
                    Texture2D contourTexture = VectorUtils.ContourTexture(colorContoursAllLayers[j], Width, Height);
                    VectorUtils.SaveTextureToFile(contourTexture, "colorTex" + j + ".png");
                }
            }*/

            runOnceFinal = true;

            #endregion

            #region Draw SVG

            List<List<ContourGroup>> contourGroupsAllLayers = IdentifyGroups(idContoursAllLayers, colorContoursAllLayers); // IDENTIFY GROUPS

            List<Layer> layers = new List<Layer>();
            foreach (List<ContourGroup> cGroups in contourGroupsAllLayers) // DRAW SILHOUETTES
            {
                Layer layer = new Layer();
                foreach (ContourGroup cg in cGroups)
                {
                    SilhouetteGroup sg = new SilhouetteGroup();
                    layer.SilhouetteGroups.Add(DrawSilhouettesForGroup(cg.Contours));
                }
                layers.Add(layer);
            }
            layers.Reverse();

            SVGProject.SVGWriter writer = new SVGProject.SVGWriter(Width, Height);
            writer.WriteSilhouettesToSVG(layers, Height); // WRITING SVG FILE

            try
            {
                // writer.SaveDocument(@"C:\Users\Vidar Hartveit Ure\Desktop\forsok.svg");
                // writer.SaveDocument(@"E:\Vidar\Desktop\forsok.svg");
                writer.SaveDocument(SVGFilePath);
            }
            catch (DirectoryNotFoundException e)
            {
                Debug.Log("Error on file saving: Directory not found!");
                QuitApplication();
            }

            int layerCount = layers.Count;
            int groupCount = 0;
            int pieceCount = 0;
            int silhouetteCount = 0;
            int pointCount = 0;

            foreach(Layer l in layers)
            {
                foreach(SilhouetteGroup sg in l.SilhouetteGroups)
                {
                    groupCount++;
                    foreach(Silhouette s in sg.Silhouettes)
                    {
                        silhouetteCount++;
                        foreach(SilhouettePiece spi in s.Pieces)
                        {
                            pieceCount++;
                            foreach(ContourPixel p in spi.Points)
                            {
                                pointCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log("Number of layers: " + layerCount);
            Debug.Log("Number of silhouette groups: " + groupCount);
            Debug.Log("Number of silhouettes: " + silhouetteCount);
            Debug.Log("Number of silhouette pieces: " + pieceCount);
            Debug.Log("Number of points: " + pointCount);

            //Texture2D silhouetteTexture = LayerTexture(layers[0]);
            //SaveTextureToFile(silhouetteTexture, "siltex.png");

            QuitApplication();

            #endregion
        }
    }

    public void ReleaseBuffers()
    {
        if (nodeBuffer != null && head != null && counter != null)
        {
            nodeBuffer.Release();
            head.Release();
            counter.Release();
        }
    }

    public void QuitApplication()
    {
        ReleaseBuffers();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    // Creates a per-pixel linked list with a node buffer, head pointer buffer and counter
    public void GeneratePerPixelLinkedList()
    {
        nodeBuffer = new ComputeBuffer((Width * Height) * perPixelLinkedListDepth, 20 * sizeof(float) + sizeof(uint), ComputeBufferType.Default);
        nodeBuffer.SetCounterValue(0);

        head = new ComputeBuffer((Width * Height), sizeof(uint), ComputeBufferType.Raw);
        head.SetData(initHead);

        counter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
        counter.SetCounterValue(0);

        Graphics.ClearRandomWriteTargets();

        Graphics.SetRandomWriteTarget(1, nodeBuffer);
        Graphics.SetRandomWriteTarget(2, head);
        Graphics.SetRandomWriteTarget(3, counter);

        Shader.SetGlobalBuffer("list", nodeBuffer);
        Shader.SetGlobalBuffer("head", head);

        Shader.SetGlobalInt("width", Width);

        Shader.SetGlobalVector("_OneDividedByRenderBufferSize", new Vector4(1.0f / Width, 1.0f / Height, 0.0f, 0.0f));
        Shader.SetGlobalFloat("_PixelZSize", pixelZSizeOffset);

        Shader.SetGlobalFloat("_PixelStride", pixelStride);
        Shader.SetGlobalFloat("_PixelStrideZCuttoff", pixelStrideZCutoff);
        Shader.SetGlobalFloat("_PixelZSize", pixelZSizeOffset);

        Matrix4x4 trs = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f));
        Matrix4x4 scrScale = Matrix4x4.Scale(new Vector3(Width, Height, 1.0f));
        Matrix4x4 projection = _camera.projectionMatrix;

        Matrix4x4 m = scrScale * trs * projection;

        Shader.SetGlobalVector("_RenderBufferSize", new Vector4(Width, Height, 0.0f, 0.0f));
        Shader.SetGlobalVector("_OneDividedByRenderBufferSize", new Vector4(1.0f / Width, 1.0f / Height, 0.0f, 0.0f));
        Shader.SetGlobalMatrix("_CameraProjectionMatrix", m);
        Shader.SetGlobalMatrix("_CameraInverseProjectionMatrix", projection.inverse);
        Shader.SetGlobalMatrix("_NormalMatrix", _camera.worldToCameraMatrix);

        Shader.SetGlobalVector("resolution", new Vector2(Width, Height));

        Shader.SetGlobalInt("_Refinement", Refinement == true ? 1 : 0);

        float nearClip = camA.nearClipPlane;
        Matrix4x4 view = camA.worldToCameraMatrix;
        Matrix4x4 p = GL.GetGPUProjectionMatrix(camA.projectionMatrix, false);
        Matrix4x4 VP = p * view;

        Shader.SetGlobalFloat("_nearClip", nearClip);
        Shader.SetGlobalFloat("_nearClip", nearClip);

        if (camA == null)
        {
            GameObject newCamA = new GameObject("New Per-Pixel-Linked-List-Cam");
            newCamA.transform.parent = transform;
            newCamA.transform.localPosition = Vector3.zero;
            newCamA.hideFlags = HideFlags.DontSave;
            camA = newCamA.AddComponent<Camera>();
        }

        camA.CopyFrom(_camera);
        camA.renderingPath = RenderingPath.DeferredShading;
        camA.enabled = false;
        camA.SetReplacementShader(Shader.Find("Hidden/PerPixelLinkedList"), null);
        camA.backgroundColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        camA.clearFlags = CameraClearFlags.SolidColor;
    }

    // Takes a render texture and returns a list of contours extracted from the texture
    public List<Contour> ExtractLayerContours(Texture2D texture)
    {
        List<Contour> contours = new List<Contour>();
        for (int pX = 0; pX < Width; pX++)
        {
            for (int pY = 0; pY < Height; pY++)
            {
                Color c = texture.GetPixel(pX, pY);
                if (c != Color.black)
                {
                    if (VectorUtils.IsEdgePixel(texture, c, pX, pY))
                    {
                        Contour foundContour = VectorUtils.FoundContour(contours, c);

                        ContourPixel contourPixel = new ContourPixel
                        {
                            Color = c,
                            XCoord = pX,
                            YCoord = pY,
                            Visited = false
                        };

                        if (foundContour == null)
                        {
                            foundContour = new Contour(Width, Height);

                            foundContour.Pixels[pX, pY] = contourPixel;
                            foundContour.Color = c;
                            foundContour.NumberOfPixels++;
                            contours.Add(foundContour);
                        }

                        foundContour.Pixels[pX, pY] = contourPixel;
                        foundContour.Color = c;
                        foundContour.NumberOfPixels++;
                    }
                }
            }
        }
        return contours;
    }

    // Takes the layers of ID contours and color contours, adds the color contours into the correct groups and returns these groups
    public List<List<ContourGroup>> IdentifyGroups(List<List<Contour>> idContourLayers, List<List<Contour>> colorContourLayers)
    {
        List<List<ContourGroup>> cGroupsAllLayers = new List<List<ContourGroup>>();
        int correctID = -1;

        for (int i = 0; i < colorContourLayers.Count; i++) // All color contour layers
        {
            List<ContourGroup> contourGroupsCurrentLayer = new List<ContourGroup>();
            AssignIDsToContours(idContourLayers[i]);
            PopulateGroups(contourGroupsCurrentLayer, idContourLayers[i].Count);
            for (int j = 0; j < colorContourLayers[i].Count; j++) // All color contours in current layer
            {
                correctID = FindCorrectGroupID(colorContourLayers[i][j], idContourLayers[i]);
                AddToGroupByID(colorContourLayers[i][j], correctID, contourGroupsCurrentLayer);

            }
            cGroupsAllLayers.Add(contourGroupsCurrentLayer);
        }
        return cGroupsAllLayers;
    }

    // Searches through a list of contour groups and adds the contour to the correct one based on ID
    private void AddToGroupByID(Contour contour, int id, List<ContourGroup> groups)
    {
        foreach (ContourGroup vg in groups)
        {
            if (vg.ID == id)
            {
                vg.Contours.Add(contour);
            }
        }
    }

    // Assigns ID values to a list of contours
    private void AssignIDsToContours(List<Contour> contours)
    {
        for (int i = 0; i < contours.Count; i++)
        {
            contours[i].ID = i;
        }
    }

    // Populates a group with contours
    private void PopulateGroups(List<ContourGroup> groups, int count)
    {
        for (int i = 0; i < count; i++)
        {
            groups.Add(new ContourGroup(i));
        }
    }

    // Takes a color contour and all the ID contours in a layer, and finds the correct ID
    public int FindCorrectGroupID(Contour colorContour, List<Contour> idContourLayer)
    {
        int correctID = -1;
        ContourPixel dummyPixel = new ContourPixel
        {
            IsDummy = true
        };
        ContourPixel someColorContourPixel = FindNextStartPixel(colorContour, dummyPixel); // Enough to check one pixel
        for (int i = 0; i < idContourLayer.Count; i++)
        {
            if (IsWithinIDContour(someColorContourPixel, idContourLayer[i]))
            {
                correctID = idContourLayer[i].ID;
            }
        }
        return correctID;
    }

    // Takes a pixel sample from a color contour and checks for the four cases that determine whether or not it is within an ID contour
    private bool IsWithinIDContour(ContourPixel colorContourPixel, Contour idContour)
    {
        bool left = false;
        bool above = false;
        bool right = false;
        bool below = false;

        foreach (ContourPixel cp in idContour.Pixels) // Every pixel in the current id contour
        {
            if (left == true && above == true && right == true && below == true) // All the conditions are met
            {
                return true;
            }
            if (cp != null)
            {
                if (colorContourPixel.XCoord == cp.XCoord && colorContourPixel.YCoord == cp.YCoord) // The pixel is exactly on the ID contour
                {
                    return true;
                }
                else if (cp.XCoord < colorContourPixel.XCoord) // Contour is to the left of the pixel
                {
                    left = true;
                }
                else if (cp.YCoord > colorContourPixel.YCoord) // Contour is above the pixel
                {
                    above = true;
                }
                else if (cp.XCoord > colorContourPixel.XCoord) // Contour is to the right of the pixel
                {
                    right = true;
                }
                else if (cp.YCoord < colorContourPixel.YCoord) // Contour is below the pixel
                {
                    below = true;
                }
            }
        }
        return false;
    }

    // Takes a group of contours and draws a silhouette for each contour in the group
    public SilhouetteGroup DrawSilhouettesForGroup(List<Contour> contours)
    {
        SilhouetteGroup sGroup = new SilhouetteGroup();

        foreach (Contour c in contours)
        {
            Silhouette silhouette = DrawSilhouette(c);

            RemovePixelNoise(silhouette);

            for (int i = 0; i < SilhouetteMergingIterations; i++)
            {
                MergeSilhouettePieces(silhouette);
            }

            sGroup.Silhouettes.Add(silhouette);
        }
        return sGroup;
    }

    // Removes silhouettes drawn from pixel noise (bug)
    public void RemovePixelNoise(Silhouette silhouette)
    {
        //Debug.Log("Silhouette size before: " + silhouette.Pieces.Count);
        foreach (SilhouettePiece sP in silhouette.Pieces)
        {
            if (sP.Points.Count < 20) // Remove the silhouette piece if it's only a few points in size (possible image noise)
            {
                sP.ShouldBeRemoved = true;
            }
        }

        for (int i = 0; i < silhouette.Pieces.Count; i++)
        {
            if (silhouette.Pieces[i].ShouldBeRemoved)
            {
                silhouette.Pieces.Remove(silhouette.Pieces[i]);
            }
        }
        //Debug.Log("Silhouette size after: " + silhouette.Pieces.Count);
    }

    // Takes a contour and draws its silhouette
    public Silhouette DrawSilhouette(Contour contour)
    {
        Silhouette silhouette = new Silhouette(contour.Color);

        int pixelCount = 0;

        ContourPixel dummyPixel = new ContourPixel
        {
            IsDummy = true
        };

        int sX, sY, nX, nY;

        ContourPixel startPixel = FindNextStartPixel(contour, dummyPixel);
        ContourPixel nextPixel = startPixel;

        bool started;

        //try
        //{

        while (startPixel != null && pixelCount != contour.NumberOfPixels)
        {
            started = false;

            // Add the first pixel of the new silhouette piece
            SilhouettePiece silhouettePiece = new SilhouettePiece();
            silhouettePiece.Points.AddFirst(startPixel);

            sX = startPixel.XCoord;
            sY = startPixel.YCoord;

            nX = startPixel.XCoord;
            nY = startPixel.YCoord;

            // Check if outside window
            //---------------------------------------------------------------------------------------------
            if (VectorUtils.AdjustIteration(contour, nX, nY) == "go-left-or-right")
            {
                //Debug.Log("Over eller under");
                if ((contour.Pixels[nX - 1, nY] != null) && !contour.Pixels[nX - 1, nY].Visited)
                {
                    nX = nX - 1;
                }
                else if ((contour.Pixels[nX + 1, nY] != null) && !contour.Pixels[nX + 1, nY].Visited)
                {
                    nX = nX + 1;
                }
            }
            else if (VectorUtils.AdjustIteration(contour, nX, nY) == "go-up-or-right")
            {
                //Debug.Log("Venstre");
                if ((contour.Pixels[nX, nY + 1] != null) && !contour.Pixels[nX, nY + 1].Visited)
                {
                    nY = nY + 1;
                }
                else if ((contour.Pixels[nX + 1, nY] != null) && !contour.Pixels[nX + 1, nY].Visited)
                {
                    nX = nX + 1;
                }
            }
            else if (VectorUtils.AdjustIteration(contour, nX, nY) == "go-up-or-left")
            {
                //Debug.Log("Høyre");
                if ((contour.Pixels[nX, nY + 1] != null) && !contour.Pixels[nX, nY + 1].Visited)
                {
                    nY = nY + 1;
                }
                else if ((contour.Pixels[nX - 1, nY] != null) && !contour.Pixels[nX - 1, nY].Visited)
                {
                    nX = nX - 1;
                }
            }
            //---------------------------------------------------------------------------------------------

            while (VectorUtils.MoreToDraw(contour, nX, nY) && (!(nX == sX && nY == sY) || !started))
            {
                //previousPixel = nextPixel;

                nextPixel = StepToNextPixel(contour, nX, nY, nextPixel);

                nX = nextPixel.XCoord;
                nY = nextPixel.YCoord;
                pixelCount++;

                silhouettePiece.Points.AddLast(nextPixel);

                started = true;
            }

            startPixel = FindNextStartPixel(contour, startPixel);
            silhouette.Pieces.Add(silhouettePiece);
        }

        /*} catch (Exception e)
        {
            return silhouettes;
        }*/

        return silhouette;
    }

    // Finds a start pixel on the contour where the silhouette drawing can be started/resumed
    private ContourPixel FindNextStartPixel(Contour contour, ContourPixel currentStart)
    {
        int x, y;

        if (currentStart.IsDummy)
        {
            x = 0;
            y = 0;
        }
        else
        {
            x = currentStart.XCoord;
            y = currentStart.YCoord;
        }

        ContourPixel newStart = null;

        while (x < Width && newStart == null)
        {
            y = 0;
            while (y < Height && newStart == null)
            {
                ContourPixel contourPixel = contour.Pixels[x, y];
                if (contourPixel != null && !contourPixel.Visited)
                {
                    contour.Pixels[x, y].Visited = true;
                    newStart = contour.Pixels[x, y];
                    newStart.IsStart = true;
                }
                y++;
            }
            x++;
        }
        return newStart;
    }

    // Steps to the next pixel in the contour that should be drawn in the silhouette
    private ContourPixel StepToNextPixel(Contour contour, int nX, int nY, ContourPixel nextPixel)
    {
        //ContourPixel nextPixel = null;
        ContourPixel thisPixel;
        if (contour.Pixels[nX, nY + 1] != null && !contour.Pixels[nX, nY + 1].Visited)
        {
            //Debug.Log("nX, nY+1");

            thisPixel = contour.Pixels[nX, nY + 1];

            contour.Pixels[nX, nY + 1].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX - 1, nY] != null && !contour.Pixels[nX - 1, nY].Visited)
        {
            //Debug.Log("nX-1, nY");

            thisPixel = contour.Pixels[nX - 1, nY];

            contour.Pixels[nX - 1, nY].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX + 1, nY] != null && !contour.Pixels[nX + 1, nY].Visited)
        {
            //Debug.Log("nX+1, nY");

            thisPixel = contour.Pixels[nX + 1, nY];

            contour.Pixels[nX + 1, nY].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX, nY - 1] != null && !contour.Pixels[nX, nY - 1].Visited)
        {
            //Debug.Log("nX, nY-1");

            thisPixel = contour.Pixels[nX, nY - 1];

            contour.Pixels[nX, nY - 1].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX + 1, nY + 1] != null && !contour.Pixels[nX + 1, nY + 1].Visited)
        {
            //Debug.Log("nX+1, nY+1");

            thisPixel = contour.Pixels[nX + 1, nY + 1];

            contour.Pixels[nX + 1, nY + 1].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX - 1, nY + 1] != null && !contour.Pixels[nX - 1, nY + 1].Visited)
        {
            //Debug.Log("nX - 1, nY + 1");

            thisPixel = contour.Pixels[nX - 1, nY + 1];

            contour.Pixels[nX - 1, nY + 1].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX + 1, nY - 1] != null && !contour.Pixels[nX + 1, nY - 1].Visited)
        {
            //Debug.Log("nX+1, nY-1");

            thisPixel = contour.Pixels[nX + 1, nY - 1];

            contour.Pixels[nX + 1, nY - 1].Visited = true;
            nextPixel = thisPixel;
        }
        else if (contour.Pixels[nX - 1, nY - 1] != null && !contour.Pixels[nX - 1, nY - 1].Visited)
        {
            //Debug.Log("nX-1, nY-1");

            thisPixel = contour.Pixels[nX - 1, nY - 1];

            contour.Pixels[nX - 1, nY - 1].Visited = true;
            nextPixel = thisPixel;
        }
        return nextPixel;
    }

    // Merges all pieces in a silhouette that have neighboring end points with each other
    public void MergeSilhouettePieces(Silhouette silhouette)
    {
        int threshold = SilhouetteMergingThreshold; // Maximum distance between two piece endpoints

        for (int i = 0; i < silhouette.Pieces.Count-1; i++)
        {
            for (int j = i + 1; j < silhouette.Pieces.Count; j++)
            {
                SilhouettePiece sp1 = silhouette.Pieces[i];
                SilhouettePiece sp2 = silhouette.Pieces[j];

                LinkedListNode<ContourPixel>[] neighbors = VectorUtils.NeighboringStartOrEndPoints(sp1, sp2, threshold);
                if (neighbors[1] != null)
                {
                    //Debug.Log("Merging...");
                    silhouette.Pieces.Remove(sp1);
                    silhouette.Pieces.Remove(sp2);

                    SilhouettePiece mergedPiece = new SilhouettePiece();

                    LinkedListNode<ContourPixel> currentNode;
                    LinkedListNode<ContourPixel> nodeCopy;

                    if (neighbors[0].Next == null)
                    {
                        //Debug.Log("First piece 1");
                        currentNode = sp1.Points.First;
                        while (currentNode.Value != neighbors[0].Value) // Add all points up to first connecting point
                        {
                            nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                            mergedPiece.Points.AddLast(nodeCopy);
                            currentNode = currentNode.Next;
                        }
                        nodeCopy = new LinkedListNode<ContourPixel>(neighbors[0].Value);
                        mergedPiece.Points.AddLast(nodeCopy); // Add first connecting point
                        currentNode = currentNode.Next;

                        nodeCopy = new LinkedListNode<ContourPixel>(neighbors[1].Value);
                        mergedPiece.Points.AddLast(nodeCopy); // Add second connecting point

                        if (neighbors[1].Next == null)
                        {
                           //Debug.Log("Second piece 1");
                            currentNode = sp2.Points.Last;
                            while (currentNode != null) // Add the rest of the points in the other piece
                            {
                                nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                                mergedPiece.Points.AddLast(nodeCopy);
                                currentNode = currentNode.Previous;
                            }
                        }
                        else // neighbors[1].Previous == null
                        {
                            //Debug.Log("Second piece 1");
                            currentNode = sp2.Points.First;
                            while (currentNode != null) // Add the rest of the points in the other piece
                            {
                                nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                                mergedPiece.Points.AddLast(nodeCopy);
                                currentNode = currentNode.Next;
                            }
                        }
                    }
                    else // (n.previous == null)
                    {
                        //Debug.Log("First piece 2");
                        currentNode = sp1.Points.Last;
                        while (currentNode.Value != neighbors[0].Value) // Add all points up to first connecting point
                        {
                            nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                            mergedPiece.Points.AddLast(nodeCopy);
                            currentNode = currentNode.Previous;
                        }
                        nodeCopy = new LinkedListNode<ContourPixel>(neighbors[0].Value);
                        mergedPiece.Points.AddLast(nodeCopy); // Add first connecting point
                        currentNode = currentNode.Previous;

                        nodeCopy = new LinkedListNode<ContourPixel>(neighbors[1].Value);
                        mergedPiece.Points.AddLast(nodeCopy); // Add second connecting point

                        if (neighbors[1].Next == null)
                        {
                            //Debug.Log("Second piece 2");
                            currentNode = sp2.Points.Last;
                            while (currentNode != null) // Add the rest of the points in the other piece
                            {
                                nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                                mergedPiece.Points.AddLast(nodeCopy);
                                currentNode = currentNode.Previous;
                            }
                        }
                        else // neighbors[1].Previous == null
                        {
                            //Debug.Log("Second piece 2");
                            currentNode = sp2.Points.First;
                            while (currentNode != null) // Add the rest of the points in the other piece
                            {
                                nodeCopy = new LinkedListNode<ContourPixel>(currentNode.Value);
                                mergedPiece.Points.AddLast(nodeCopy);
                                currentNode = currentNode.Next;
                            }
                        }
                    }
                    //Debug.Log("Added");
                    silhouette.Pieces.Add(mergedPiece);
                }
            }
        }
    }

    // Checks which direction this pixel is visited from
    public string FindVisitedFrom(ContourPixel previousPixel, ContourPixel thisPixel)
    {
        string visitedFrom = "";
        if (previousPixel.XCoord < thisPixel.XCoord)
        {
            visitedFrom = "Left";
        }
        else if (previousPixel.XCoord > thisPixel.XCoord)
        {
            visitedFrom = "Right";
        }
        else if (previousPixel.YCoord < thisPixel.YCoord)
        {
            visitedFrom = "Below";
        }
        else if (previousPixel.YCoord > thisPixel.YCoord)
        {
            visitedFrom = "Above";
        }
        return visitedFrom;
    }
}
