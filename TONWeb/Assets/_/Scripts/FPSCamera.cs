using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSCamera : MonoBehaviour
{
    float _rotationX = 0;
    
    public float cameraSpeed = 9;
    public float min = -45;
    public float max = 45;

    // Update is called once per frame
    void Update()
    {
        if (!Helper.moveLock && !Helper.mobile)
        {
            //Получает данные, вводимые с помощью мыши
            _rotationX -= Input.GetAxis("Mouse Y") * cameraSpeed;
            //Ограничивает значение между минимальным и максимальным
            _rotationX = Mathf.Clamp(_rotationX, min, max);
            float rotationY = transform.localEulerAngles.y;
            transform.localEulerAngles = new Vector3(_rotationX, rotationY, 0);
        }
    }
}
