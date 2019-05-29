using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Contour {

    private ContourPixel[,] _pixels;
    private Color _color;
    private int _numberOfPixels;
    private int _id;

    public ContourPixel[,] Pixels { get { return _pixels; } set { _pixels = value; } }
    public Color Color { get { return _color; } set { _color = value; } }
    public int NumberOfPixels { get { return _numberOfPixels; } set { _numberOfPixels = value; } }
    public int ID { get { return _id; } set { _id = value; } }

    public Contour(int width, int height)
    {
        _pixels = new ContourPixel[width, height];
        _numberOfPixels = 0;
    }
}
