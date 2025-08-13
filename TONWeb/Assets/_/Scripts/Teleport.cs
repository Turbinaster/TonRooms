using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teleport : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "Player") { PluginJS.ShowFriendsTeleport(); }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name == "Player") PluginJS.HideFriendsTeleport();
    }
}
