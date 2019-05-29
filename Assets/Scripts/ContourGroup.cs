using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContourGroup
{
    private List<Contour> _contours;
    private int _id;

    public List<Contour> Contours { get { return _contours; } set { _contours = value; } }
    public int ID { get { return _id; } set { _id = value; } }

    public ContourGroup(int id)
    {
        _contours = new List<Contour>();
        _id = id;
    }
}
