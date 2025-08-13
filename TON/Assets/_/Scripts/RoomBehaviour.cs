using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class RoomBehaviour : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var cell = transform.parent.parent.GetComponent<HexCell>();
        if (cell != null)
        {
            var player = other.GetComponent<PlayerBehaviour>();
            if (player != null)
            {
                if (string.IsNullOrEmpty(cell.coordinates.address)) player.coordinates = cell.coordinates;
                else player.coordinates = null;
                player.ShowCoords();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var cell = GetComponentInChildren<HexCell>();
        if (cell != null)
        {
            var player = other.GetComponent<PlayerBehaviour>();
            if (player != null && player.coordinates == cell.coordinates)
            {
                player.coordinates = null;
                player.ShowCoords();
            }
        }
    }
}
