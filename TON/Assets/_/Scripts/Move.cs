using UnityEngine;

//“ребует предварительного подключени€ компонента
[RequireComponent(typeof(CharacterController))]
public class Move : MonoBehaviour
{
    public float speed = 3;
    public float gravity = -9.8f;
    public float deltaX;
    public float deltaZ;

    public float speedLook = 9;
    public float min = -45;
    public float max = 45;
    float _rotationX = 0;

    public float speedLook1 = 9;

    CharacterController _char;

    void Start()
    {
        _char = GetComponent<CharacterController>();

        //—крывает указатель мыши в центре экрана. ѕоказать его можно клавишей Esc
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    void Update()
    {
        //"Horizontal" и "Vertical" Ч это дополнительные имена дл€ сопоставлени€ с клавиатурой.
        deltaX = Input.GetAxis("Horizontal") * speed;
        deltaZ = Input.GetAxis("Vertical") * speed;
        var move = new Vector3(deltaX, 0, deltaZ);
        //ќграничивает модуль вектора скоростью движени€, чтобы движение по диагонали не происходило быстрее движени€ вдоль координатных осей
        move = Vector3.ClampMagnitude(move, speed);
        //Ќа игрока действует посто€нна€ сила, т€нуща€ его вниз.
        //„тобы его т€нуло всегда вниз в глобальном пространстве, нужно его ограничить горизонтальным вращением,
        //а прив€занную к нему камеру ограничить вертикальным вращением
        //move.y = gravity;
        //Ёто врем€ между кадрами. ”множение скорости на эту переменную приведет к одинаковой скорости на всех устройствах
        move *= Time.deltaTime;
        //ѕреобразование от локальных к глобальным координатам
        move = transform.TransformDirection(move);
        //ѕеремещение объекта со св€занным компонентом CharacterController. ћетоду следует передавать вектор, определенный в глобальном пространстве
        _char.Move(move);

        //ѕолучает данные, вводимые с помощью мыши
        _rotationX -= Input.GetAxis("Mouse Y") * speedLook;
        //ќграничивает значение между минимальным и максимальным
        _rotationX = Mathf.Clamp(_rotationX, min, max);
        float rotationY = transform.localEulerAngles.y;
        transform.localEulerAngles = new Vector3(_rotationX, rotationY, 0);

        //¬ращение объекта с помощью мыши по горизонтали
        transform.Rotate(0, Input.GetAxis("Mouse X") * speedLook1, 0);
    }
}
