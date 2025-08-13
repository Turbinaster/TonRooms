using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexCell : MonoBehaviour
{
    [HideInInspector]
    public HexCoordinates coordinates = new HexCoordinates(0, 0);
    [HideInInspector]
    public List<float> w = new List<float>();
    [HideInInspector]
    public List<float> h = new List<float>();
    [HideInInspector]
    public float maxHeight = 0;
    public int index, floors;
    public bool rotate;
}
