using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SilhouetteGroup
{
    private List<Silhouette> _silhouettes;

    public List<Silhouette> Silhouettes { get { return _silhouettes; } set { _silhouettes = value; } }

    public SilhouetteGroup()
    {
        _silhouettes = new List<Silhouette>();
    }
}
