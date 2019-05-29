using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContourPixel
{
    private Color _color;
    private int _xCoord;
    private int _yCoord;
    private bool _visited;
    private bool _isDummy;
    private bool _isStart;
    private string _visitedFrom;


    public Color Color { get { return _color; } set { _color = value; } }
    public int XCoord { get { return _xCoord; } set { _xCoord = value; } }
    public int YCoord { get { return _yCoord; } set { _yCoord = value; } }
    public bool Visited { get { return _visited; } set { _visited = value; } }
    public bool IsDummy { get { return _isDummy; } set { _isDummy = value; } }
    public bool IsStart { get { return _isStart; } set { _isStart = value; } }
    public string VisitedFrom { get { return _visitedFrom; } set { _visitedFrom = value; } }

    public ContourPixel()
    {
        _color = new Color();
        _xCoord = 0;
        _yCoord = 0;
        _isDummy = false;
        _isStart = false;
        _visitedFrom = "";
    }
}
