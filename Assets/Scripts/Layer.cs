using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Layer {

    private List<SilhouetteGroup> _silhouetteGroups;

    public List<SilhouetteGroup> SilhouetteGroups { get { return _silhouetteGroups; } set { _silhouetteGroups = value; } }

    public Layer()
    {
        _silhouetteGroups = new List<SilhouetteGroup>();
    }
}
