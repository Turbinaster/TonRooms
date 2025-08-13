using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerBehaviour : MonoBehaviour
{
    public HexCoordinates coordinates;
    public int x, y, z, floors = 0;
    Init init;
    ThirdPersonController tpc;

    void Awake()
    {
        init = GameObject.Find("Init").GetComponent<Init>();
        tpc = GetComponent<ThirdPersonController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            Cursor.lockState = CursorLockMode.None;
            tpc.LockCameraPosition = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            Cursor.lockState = CursorLockMode.Locked;
            tpc.LockCameraPosition = false;
        }
    }

    public void ShowCoords()
    {
        x = coordinates?.x ?? 0;
        y = coordinates?.y ?? 0;
        z = coordinates?.z ?? 0;

        if (x != 0 && y != 0 && z != 0) init.ButtonAuth.SetActive(true);
        else init.ButtonAuth.gameObject.SetActive(false);
    }

    public int Floor()
    {
        return Mathf.RoundToInt(transform.position.y / 5);
    }
}
