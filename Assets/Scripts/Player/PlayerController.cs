using Mirror;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : NetworkBehaviour, IControllable
{
    [Header("Function Options")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool canCrouch = true;
    [SerializeField] private bool canUseHeadbob = true;
    [SerializeField] private bool useFootsteps = true;

    [Header("Movement Parameters")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private float crouchSpeed = 1.5f;

    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)] private float lookSpeedX = 2.0f;
    [SerializeField, Range(1, 10)] private float lookSpeedY = 2.0f;
    [SerializeField, Range(1, 100)] private float upperLookLimit = 80.0f;
    [SerializeField, Range(1, 100)] private float lowerLookLimit = 80.0f;

    [Header("Jumping Parameters")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float gravity = 30.0f;

    [Header("Crouch Parameters")]
    [SerializeField] private float crouchHeight = 0.5f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float timeToCrouch = 0.25f;
    [SerializeField] private Vector3 crouchingCenter = new Vector3(0, 0.5f, 0);
    [SerializeField] private Vector3 standingCenter = new Vector3(0, 0, 0);
    private bool isCrouching;
    private bool duringCrouchAnimation;

    [Header("Headbob Parameters")]
    [SerializeField] private float walkBobSpeed = 14f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float sprintBobSpeed = 18f;
    [SerializeField] private float sprintBobAmount = 0.1f;
    [SerializeField] private float crouchBobSpeed = 8f;
    [SerializeField] private float crouchBobAmount = 0.025f;
    private float defaultYPos = 0;
    private float timer;

    [Header("Footstep Parameters")]
    [SerializeField] private float baseStepSpeed = 0.5f;
    [SerializeField] private float crouchStepMultiplier = 1.5f;
    [SerializeField] private float sprintStepMultiplier = 0.6f;
    [SerializeField] private AudioSource footstepAudioSource = default;
    [SerializeField] private AudioClip[] woodClips = default;
    [SerializeField] private AudioClip[] metalClips = default;
    [SerializeField] private AudioClip[] grassClips = default;
    private float footstepTimer = 0;

    private float GetCurrentOffset => isCrouching ? baseStepSpeed * crouchStepMultiplier : IsSprinting ? baseStepSpeed * sprintStepMultiplier : baseStepSpeed;

    private Camera playerCamera;
    private CharacterController characterController;

    private Vector3 moveDirection;
    private Vector2 currentInput;
    private float rotationX = 0;

    private bool isSprinting;
    private bool jumpRequested;

    public bool IsSprinting => canSprint && isSprinting;

    void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        characterController = GetComponent<CharacterController>();
        defaultYPos = playerCamera.transform.localPosition.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GetComponent<PlayerInputHandler>().SetControllable(this);
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            playerCamera.gameObject.SetActive(false);
            return;
        }

        if (jumpRequested)
        {
            moveDirection.y = jumpForce;
            jumpRequested = false;
        }

        if (canUseHeadbob)
            HandleHeadbob();

        if (useFootsteps)
            HandleFootsteps();

        ApplyFinalMovements();
    }

    public void Move(Vector2 input)
    {
        float speed = isCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
        currentInput = new Vector2(speed * input.y, speed * input.x);
    }

    public void Look(Vector2 input)
    {
        rotationX -= input.y * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, input.x * lookSpeedX, 0);
    }

    public void Jump()
    {
        if (canJump && characterController.isGrounded && !duringCrouchAnimation)
        {
            jumpRequested = true;
        }
    }

    public void SetSprint(bool sprint)
    {
        isSprinting = sprint;
    }

    public void ToggleCrouch()
    {
        if (!duringCrouchAnimation && characterController.isGrounded)
        {
            StartCoroutine(CrouchStand());
        }
    }

    private void HandleHeadbob()
    {
        if (!characterController.isGrounded) return;

        if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
        {
            timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : IsSprinting ? sprintBobSpeed : walkBobSpeed);
            float bobAmount = isCrouching ? crouchBobAmount : IsSprinting ? sprintBobAmount : walkBobAmount;
            Vector3 camPos = playerCamera.transform.localPosition;
            camPos.y = defaultYPos + Mathf.Sin(timer) * bobAmount;
            playerCamera.transform.localPosition = camPos;
        }
    }

    private void ApplyFinalMovements()
    {
        if (!characterController.isGrounded)
            moveDirection.y -= gravity * Time.deltaTime;

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleFootsteps()
    {
        if (!characterController.isGrounded) return;
        if (currentInput == Vector2.zero) return;

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0)
        {
            if (Physics.Raycast(playerCamera.transform.position, Vector3.down, out RaycastHit hit, 3))
            {
                AudioClip clip = null;

                switch (hit.collider.tag)
                {
                    case "Footsteps/WOOD":
                        if (woodClips != null && woodClips.Length > 0)
                            clip = woodClips[Random.Range(0, woodClips.Length)];
                        break;
                    case "Footsteps/METAL":
                        if (metalClips != null && metalClips.Length > 0)
                            clip = metalClips[Random.Range(0, metalClips.Length)];
                        break;
                    case "Footsteps/GRASS":
                        if (grassClips != null && grassClips.Length > 0)
                            clip = grassClips[Random.Range(0, grassClips.Length)];
                        break;
                    default:
                        if (woodClips != null && woodClips.Length > 0)
                            clip = woodClips[Random.Range(0, woodClips.Length)];
                        break;
                }

                if (clip != null)
                {
                    footstepAudioSource.pitch = Random.Range(0.88f, 1);
                    footstepAudioSource.PlayOneShot(clip);
                }
            }
            footstepTimer = GetCurrentOffset;
        }
    }

    private IEnumerator CrouchStand()
    {
        if (isCrouching && Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f))
            yield break;

        duringCrouchAnimation = true;

        float timeElapsed = 0;
        float targetHeight = isCrouching ? standingHeight : crouchHeight;
        float currentHeight = characterController.height;
        Vector3 targetCenter = isCrouching ? standingCenter : crouchingCenter;
        Vector3 currentCenter = characterController.center;

        while (timeElapsed < timeToCrouch)
        {
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
            characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        characterController.height = targetHeight;
        characterController.center = targetCenter;

        isCrouching = !isCrouching;
        duringCrouchAnimation = false;
    }
}