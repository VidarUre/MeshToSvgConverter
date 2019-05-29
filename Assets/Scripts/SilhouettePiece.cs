using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SilhouettePiece
{
    private LinkedList<ContourPixel> _points;
    private bool _shouldBeRemoved;

    public LinkedList<ContourPixel> Points { get { return _points; } set { _points = value; } }
    public bool ShouldBeRemoved { get { return _shouldBeRemoved; } set { _shouldBeRemoved = value; } }

    public SilhouettePiece()
    {
        _points = new LinkedList<ContourPixel>();
        _shouldBeRemoved = false;
    }
}
