using UnityEngine;
using UnityEngine.InputSystem;

public class TESTDELETEAFTER : MonoBehaviour
{
    [Header("Настройки")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;

    private CharacterController controller;
    private Camera playerCamera;
    private Vector2 moveInput;
    private Vector2 mouseDelta;
    private float verticalAngle = 0f;   // ← новая переменная

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
            Debug.LogWarning("Камера не найдена!");

        Cursor.lockState = CursorLockMode.Locked;
        verticalAngle = 0f;  // начальный взгляд прямо
    }

    private void Update()
    {
        // ---- ввод WASD ----
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            moveInput = move.normalized;
        }

        // ---- ввод мыши ----
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            mouseDelta = mouse.delta.ReadValue() * mouseSensitivity * Time.deltaTime;
        }

        // ---- поворот игрока (горизонталь) ----
        transform.Rotate(Vector3.up, mouseDelta.x);

        // ---- поворот камеры (вертикаль) ----
        verticalAngle -= mouseDelta.y;
        verticalAngle = Mathf.Clamp(verticalAngle, -90f, 90f);
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(verticalAngle, 0f, 0f);

        // ---- движение ----
        Vector3 moveDirection = (transform.forward * moveInput.y + transform.right * moveInput.x) * moveSpeed * Time.deltaTime;
        controller.Move(moveDirection);
    }
}