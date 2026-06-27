using Mirror;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -15f;

    [Header("Head Bob")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float sprintBobMultiplier = 1.5f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float crouchHeight = 0.6f;
    [SerializeField] private float standingHeight = 1.8f;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] concreteFootsteps;
    [SerializeField] private AudioClip[] metalFootsteps;
    [SerializeField] private AudioClip[] woodFootsteps;
    [SerializeField] private float footstepInterval = 0.5f;

    private CharacterController controller;
    private PlayerInputHandler inputHandler;
    private Vector3 velocity;
    private float currentSpeed;
    private bool isCrouching = false;
    private float footstepTimer = 0f;
    private float bobTimer = 0f;
    private float defaultYPos;
    private float defaultHeight;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        defaultHeight = controller.height;
        defaultYPos = cameraTransform.localPosition.y;
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            cameraTransform.gameObject.SetActive(false);
            return;
        }

        HandleMovement();
        HandleJump();
        HandleCrouch();
        ApplyGravity();
        HandleHeadBob();
        HandleFootsteps();
    }

    void HandleMovement()
    {
        Vector2 input = inputHandler.MoveInput;
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = move.normalized;

        if (inputHandler.IsSprinting && !isCrouching && input.y > 0.1f)
            currentSpeed = sprintSpeed;
        else if (isCrouching)
            currentSpeed = crouchSpeed;
        else
            currentSpeed = walkSpeed;

        move *= currentSpeed * Time.deltaTime;
        controller.Move(move);
    }

    void HandleJump()
    {
        if (inputHandler.IsJumping && controller.isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void HandleCrouch()
    {
        if (inputHandler.IsCrouching)
        {
            isCrouching = true;
            controller.height = Mathf.Lerp(controller.height, crouchHeight, Time.deltaTime * 10f);
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, crouchHeight * 0.5f, Time.deltaTime * 10f);
            cameraTransform.localPosition = camPos;
        }
        else
        {
            isCrouching = false;
            controller.height = Mathf.Lerp(controller.height, defaultHeight, Time.deltaTime * 10f);
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, defaultYPos, Time.deltaTime * 10f);
            cameraTransform.localPosition = camPos;
        }
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleHeadBob()
    {
        if (!controller.isGrounded || inputHandler.MoveInput.magnitude < 0.1f)
        {
            bobTimer = 0f;
            return;
        }

        float speed = inputHandler.IsSprinting ? sprintBobMultiplier : 1f;
        bobTimer += Time.deltaTime * bobSpeed * speed;

        float bobY = Mathf.Sin(bobTimer) * bobAmplitude;
        float bobX = Mathf.Cos(bobTimer / 2) * bobAmplitude * 0.5f;

        Vector3 camPos = cameraTransform.localPosition;
        float targetY = isCrouching ? crouchHeight * 0.5f : defaultYPos;
        camPos.y = targetY + bobY;
        camPos.x = bobX;
        cameraTransform.localPosition = camPos;
    }

    void HandleFootsteps()
    {
        if (!controller.isGrounded || inputHandler.MoveInput.magnitude < 0.1f)
        {
            footstepTimer = 0f;
            return;
        }

        footstepTimer += Time.deltaTime;
        float interval = footstepInterval / (currentSpeed / walkSpeed);

        if (footstepTimer >= interval)
        {
            footstepTimer = 0f;
            PlayFootstepSound();
        }
    }

    void PlayFootstepSound()
    {
        if (footstepSource == null) return;

        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.down * 0.1f, Vector3.down, out hit, 0.3f))
        {
            AudioClip clip = null;

            switch (hit.collider.tag)
            {
                case "Metal":
                    if (metalFootsteps.Length > 0)
                        clip = metalFootsteps[Random.Range(0, metalFootsteps.Length)];
                    break;
                case "Wood":
                    if (woodFootsteps.Length > 0)
                        clip = woodFootsteps[Random.Range(0, woodFootsteps.Length)];
                    break;
                default:
                    if (concreteFootsteps.Length > 0)
                        clip = concreteFootsteps[Random.Range(0, concreteFootsteps.Length)];
                    break;
            }

            if (clip != null)
            {
                footstepSource.pitch = Random.Range(0.9f, 1.1f);
                footstepSource.PlayOneShot(clip);
            }
        }
    }
}