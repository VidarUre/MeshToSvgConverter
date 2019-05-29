using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public class VectorUtils
{
    // Saves the texture as an image
    public static void SaveTextureToFile(Texture2D tex, string fileName)
    {
        var bytes = tex.EncodeToPNG();
        var file = File.Open(Application.dataPath + "/" + fileName, FileMode.Create);
        var binary = new BinaryWriter(file);
        binary.Write(bytes);
        file.Close();
    }

    // Returns a random RGB value
    public static Color RandomRGB(System.Random random)
    {
        float r = (float)random.NextDouble();
        float g = (float)random.NextDouble();
        float b = (float)random.NextDouble();
        float a = (float)random.NextDouble();
        return new Color(r, g, b, a);
    }

    // Checks if a given pixel in a texture is the edge of a shape
    public static bool IsEdgePixel(Texture2D texture, Color c, int x, int y)
    {
        return !c.Equals(texture.GetPixel(x, y + 1))
            || !c.Equals(texture.GetPixel(x + 1, y))
            || !c.Equals(texture.GetPixel(x, y - 1))
            || !c.Equals(texture.GetPixel(x - 1, y));
    }

    // Checks if there is more to draw on the contour
    public static bool MoreToDraw(Contour c, int nX, int nY)
    {
        try
        {
            return (c.Pixels[nX, nY + 1] != null && !c.Pixels[nX, nY + 1].Visited)
                || (c.Pixels[nX - 1, nY] != null && !c.Pixels[nX - 1, nY].Visited)
                || (c.Pixels[nX + 1, nY] != null && !c.Pixels[nX + 1, nY].Visited)
                || (c.Pixels[nX, nY - 1] != null && !c.Pixels[nX, nY - 1].Visited)
                || (c.Pixels[nX - 1, nY + 1] != null && !c.Pixels[nX - 1, nY + 1].Visited)
                || (c.Pixels[nX + 1, nY + 1] != null && !c.Pixels[nX + 1, nY + 1].Visited)
                || (c.Pixels[nX - 1, nY - 1] != null && !c.Pixels[nX - 1, nY - 1].Visited)
                || (c.Pixels[nX + 1, nY - 1] != null && !c.Pixels[nX + 1, nY - 1].Visited);
        }
        catch (IndexOutOfRangeException e)
        {
            return false;
        }
    }

    // Checks if there are two neighboring points in a pair of silhouette pieces, and returns them
    public static LinkedListNode<ContourPixel>[] NeighboringStartOrEndPoints(SilhouettePiece sp1, SilhouettePiece sp2, int threshold)
    {
        LinkedListNode<ContourPixel>[] neighbors = new LinkedListNode<ContourPixel>[2];

        LinkedListNode<ContourPixel> startPoint1 = sp1.Points.First;
        LinkedListNode<ContourPixel> endPoint1 = sp1.Points.Last;

        LinkedListNode<ContourPixel> startPoint2 = sp2.Points.First;
        LinkedListNode<ContourPixel> endPoint2 = sp2.Points.Last;

        if (AreNeighbors(startPoint1.Value, startPoint2.Value, threshold))
        {
            neighbors[0] = startPoint1;
            neighbors[1] = startPoint2;
        }
        else if (AreNeighbors(startPoint1.Value, endPoint2.Value, threshold))
        {
            neighbors[0] = startPoint1;
            neighbors[1] = endPoint2;
        }
        else if (AreNeighbors(endPoint1.Value, startPoint2.Value, threshold))
        {
            neighbors[0] = endPoint1;
            neighbors[1] = startPoint2;
        }
        else if (AreNeighbors(endPoint1.Value, endPoint2.Value, threshold))
        {
            neighbors[0] = endPoint1;
            neighbors[1] = endPoint2;
        }
        return neighbors;
    }

    // Checks if two pixels are neighboring
    public static bool AreNeighbors(ContourPixel p1, ContourPixel p2, int threshold)
    {
        int XDifference;
        int YDifference;

        if (p1.XCoord >= p2.XCoord)
        {
            XDifference = p1.XCoord - p2.XCoord;
        }
        else
        {
            XDifference = p2.XCoord - p1.XCoord;
        }

        if (p1.YCoord >= p2.YCoord)
        {
            YDifference = p1.YCoord - p2.YCoord;
        }
        else
        {
            YDifference = p2.YCoord - p1.YCoord;
        }

        return Enumerable.Range(1, threshold).Contains(XDifference) && Enumerable.Range(1, threshold).Contains(YDifference);
    }

    // Takes the silhouette drawing in another direction (indicated by reaching the edge of the screen)
    public static string AdjustIteration(Contour c, int nX, int nY)
    {
        string adjustment = "";
        if ((nY > c.Pixels.GetLength(1)) || (nY < 0)) // Above or below window
        {
            adjustment = "go-left-or-right";
        }
        else if (nX < 0) // Outside left side of window
        {
            adjustment = "go-up-or-right";
        }
        else if (nX > c.Pixels.GetLength(0))
        {
            adjustment = "go-up-or-left";
        }
        return adjustment;
    }

    // Looks for a contour with the given color, and returns it if it exists
    public static Contour FoundContour(List<Contour> contours, Color color)
    {
        Contour foundContour = null;
        foreach (Contour c in contours)
        {
            if (c.Color.Equals(color))
            {
                foundContour = c;
            }
        }
        return foundContour;
    }

    // Takes a list of contours and creates a texture of it
    public static Texture2D ContourTexture(List<Contour> contours, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        foreach (Contour c in contours)
        {
            foreach (ContourPixel cP in c.Pixels)
            {
                if (cP != null)
                {
                    texture.SetPixel(cP.XCoord, cP.YCoord, c.Color);
                }
            }
        }
        texture.Apply();
        return texture;
    }
}
