using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Требует предварительного подключения компонента
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    CharacterController _char;
    public float moveSpeed = 5;
    public float gravity = -9.8f;
    public float jumpHeight = 5;
    public float cameraSpeed1 = 9;

    public Vector3 playerVelocity;
    public bool grounded;
    public LayerMask groundMask;
    public float groundDistance = 2;
    float _groundDistance;

    bool _jump;

    int leftFingerId, rightFingerId;
    float halfScreenWidth;

    //Управление камерой
    Vector2 lookInput;
    float cameraPitch;
    public float cameraSensitivity = 2;

    //Движение игрока
    Vector2 moveTouchStartPosition;
    Vector2 moveInput;
    Transform cameraTransform;

    void Start()
    {
        _char = GetComponent<CharacterController>();

        //Скрывает указатель мыши в центре экрана. Показать его можно клавишей Esc
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _groundDistance = groundDistance;
        cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (Helper.mobile)
        {
            Vector2 tap = Vector2.zero;
            string touchPhase = "null";
            if (Input.touchCount > 0)
            {
                halfScreenWidth = Screen.width / 2;
                //Перебор всех обнаруженных касаний
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    tap = t.position;

                    //Проверяется каждая фаза касания
                    switch (t.phase)
                    {
                        case TouchPhase.Began:
                            touchPhase = "Began";
                            if (t.position.x < halfScreenWidth) { leftFingerId = t.fingerId; moveTouchStartPosition = t.position; }
                            else if (t.position.x > halfScreenWidth) rightFingerId = t.fingerId;
                            break;
                        case TouchPhase.Ended:
                            touchPhase = "Ended";
                            if (t.fingerId == leftFingerId) { leftFingerId = -1; moveInput = Vector2.zero; }
                            else if (t.fingerId == rightFingerId) { rightFingerId = -1; lookInput = Vector2.zero; }
                            break;
                        case TouchPhase.Canceled:
                            touchPhase = "Canceled";
                            if (t.fingerId == leftFingerId) { leftFingerId = -1; moveInput = Vector2.zero; }
                            else if (t.fingerId == rightFingerId) { rightFingerId = -1; lookInput = Vector2.zero; }
                            break;
                        case TouchPhase.Moved:
                            touchPhase = "Moved";
                            if (t.position.x < halfScreenWidth) leftFingerId = t.fingerId;
                            else if (t.position.x > halfScreenWidth) rightFingerId = t.fingerId;
                            //Обнаружение ввода для осмотра вокруг
                            if (t.fingerId == rightFingerId) lookInput = t.deltaPosition * cameraSensitivity * Time.deltaTime;
                            else if (t.fingerId == leftFingerId) moveInput = t.position - moveTouchStartPosition;
                            break;
                        case TouchPhase.Stationary:
                            touchPhase = "Stationary";
                            //Установка обзора в 0, если палец не двигается
                            if (t.fingerId == rightFingerId) lookInput = Vector2.zero;
                            break;
                    }
                }
            }
            else
            {
                tap = Vector2.zero;
                touchPhase = "null";
                leftFingerId = -1;
                rightFingerId = -1;
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
            }
            //PluginJS.SendMessageToPage1($"tap = {tap}, touchPhase = {touchPhase}, moveInput = {moveInput}, lookInput = {lookInput}");
        }
        if (!Helper.moveLock)
        {
            //"Horizontal" и "Vertical" — это дополнительные имена для сопоставления с клавиатурой.
            float deltaX = Input.GetAxis("Horizontal") * moveSpeed;
            float deltaZ = Input.GetAxis("Vertical") * moveSpeed;
            if (deltaX == 0) deltaX = moveInput.x;
            if (deltaZ == 0) deltaZ = moveInput.y;
            var move = new Vector3(deltaX, 0, deltaZ);
            //Ограничивает модуль вектора скоростью движения, чтобы движение по диагонали не происходило быстрее движения вдоль координатных осей
            move = Vector3.ClampMagnitude(move, moveSpeed);

            //Это время между кадрами. Умножение скорости на эту переменную приведет к одинаковой скорости на всех устройствах
            move *= Time.deltaTime;
            //Преобразование от локальных к глобальным координатам
            move = transform.TransformDirection(move);
            //Перемещение объекта со связанным компонентом CharacterController. Методу следует передавать вектор, определенный в глобальном пространстве
            _char.Move(move);

            //Вращение объекта с помощью мыши по горизонтали
            if (!Helper.mobile) transform.Rotate(0, Input.GetAxis("Mouse X") * cameraSpeed1, 0);
            else
            {
                //Вертикальное вращение
                cameraPitch = Mathf.Clamp(cameraPitch + lookInput.y, -90f, 90f);
                cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
                //Горизонтальное вращение
                transform.Rotate(transform.up, -lookInput.x);
            }

            if (playerVelocity.y < 0) groundDistance = _groundDistance;
            var p1 = new Vector3(transform.position.x, transform.position.y - groundDistance, transform.position.z);
            var p2 = new Vector3(transform.position.x, transform.position.y + groundDistance, transform.position.z);
            grounded = Physics.CheckCapsule(p1, p2, 0.1f, groundMask);
            if (grounded) playerVelocity.y = 0f;
            var jump = Input.GetButtonDown("Jump");
            if (!jump) jump = _jump;
            if (jump && grounded) { groundDistance = 0; playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravity); }
            playerVelocity.y += gravity * Time.deltaTime;
            _char.Move(playerVelocity * Time.deltaTime);
        }
    }

    public void MoveStop() { _jump = false; }
    /*public void MoveLeft() { _deltaX = -moveSpeed; }
    public void MoveRight() { _deltaX = moveSpeed; }
    public void MoveUp() { _deltaZ = moveSpeed; }
    public void MoveDown() { _deltaZ = -moveSpeed; }*/
    public void Jump() { _jump = true; }
    public void OK()
    {
        Helper.init.HideCursor();
        Helper.init.image.image.color = new Color32(255, 255, 255, 255);
        Helper.init.image.scale = false;
        Helper.init.jump.SetActive(true);
        Helper.init.ok.SetActive(false);
        Helper.init.image.Save();
    }
}
