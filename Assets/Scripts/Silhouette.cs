using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Silhouette
{

    private List<SilhouettePiece> _pieces;
    private Color _color;

    public List<SilhouettePiece> Pieces { get { return _pieces; } set { _pieces = value; } }
    public Color Color { get { return _color; } set { _color = value; } }

    public Silhouette(Color c)
    {
        _pieces = new List<SilhouettePiece>();
        _color = c;
    }
}
