using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Base walking speed.")]
    public float walkSpeed = 5f;
    [Tooltip("Multiplier applied to walk speed when sprinting.")]
    public float sprintMultiplier = 1.5f;
    [Tooltip("How much control the player has while in the air (0 to 1).")]
    [Range(0f, 1f)] public float airControlMultiplier = 0.5f; 

    [Header("Crouch Settings")]
    public float crouchSpeed = 2f;
    public float crouchHeight = 1.0f; // Crouched height
    public float crouchTransitionSpeed = 10f;
    public LayerMask obstacleLayer;

    [Header("Jump & Physics")]
    public float jumpHeight = 1.2f; // 1.2m 
    public int maxJumps = 2;
    public float gravity = -20f; // Higher gravity. better feeling in FPS;
    public float coyoteTimeDuration = 0.2f;

    [Header("Camera & Eyes")]
    public float lookSensitivity = 0.1f;
    public Transform playerCamera;
    [Tooltip("Percentage of height where eyes are positioned (0.9 = 90% of height).")]
    public float eyeHeightRatio = 0.9f; 

    // --- Internal State ---
    private CharacterController controller;
    private Vector3 playerVelocity; 
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float xRotation = 0f; 
    
    // Logic States
    private int jumpCount;
    private float coyoteTimeCounter;
    private bool isSprinting;
    private bool isAutoRun;
    private bool wantsToCrouch;
    private bool isCrouching;  
    
    // Cached References
    private float standingHeight; // Cache the original height from Inspector
    private Vector3 originalCenter;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Cache initial values set in Inspector to use as "Standing State"
        standingHeight = controller.height;
        originalCenter = controller.center;

        // Hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        UpdateTimers();
        HandleCrouchLogic();
        HandleRotation();
        HandleMovementPhysics();
    }

    // --- LOGIC UPDATES ---

    private void UpdateTimers()
    {
        if (controller.isGrounded)
        {
            coyoteTimeCounter = coyoteTimeDuration;
            jumpCount = 0; 
            
            if (playerVelocity.y < 0) playerVelocity.y = -2f; 
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void HandleCrouchLogic()
    {

        if(!controller.isGrounded) return;

        // Raycast logic to check for ceilings
        bool hasCeiling = Physics.Raycast(transform.position, Vector3.up, standingHeight, obstacleLayer);
        isCrouching = wantsToCrouch || hasCeiling;

        // --- 1. Calculate Target Controller Size ---
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        
        // Center needs to be half of the height to keep feet on ground
        Vector3 targetCenter = new Vector3(0, targetHeight / 2f, 0);

        // --- 2. Calculate Camera Position (Eyes) ---
        // Eyes are always at ~90% of the CURRENT height (standing or crouching)
        float currentTargetEyeHeight = targetHeight * eyeHeightRatio;
        Vector3 targetCamPos = new Vector3(0, currentTargetEyeHeight, 0);

        // --- 3. Apply Smooth Transitions ---
        float t = Time.deltaTime * crouchTransitionSpeed;
        
        controller.height = Mathf.Lerp(controller.height, targetHeight, t);
        controller.center = Vector3.Lerp(controller.center, targetCenter, t);
        playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetCamPos, t);
    }

    private void HandleRotation()
    {
        transform.Rotate(Vector3.up * lookInput.x * lookSensitivity);

        xRotation -= lookInput.y * lookSensitivity;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void HandleMovementPhysics()
    {
        float targetSpeed = walkSpeed;
        if (isCrouching) targetSpeed = crouchSpeed;
        else if (isSprinting) targetSpeed = walkSpeed * sprintMultiplier;

        float verticalInput = (isAutoRun && moveInput.y > -0.1f) ? 1f : moveInput.y;
        
        Vector3 horizontalMove = transform.right * moveInput.x + transform.forward * verticalInput;

        if (!controller.isGrounded)
        {
            horizontalMove *= airControlMultiplier;
        }

        horizontalMove *= targetSpeed;

        playerVelocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = horizontalMove + playerVelocity;
        
        controller.Move(finalMove * Time.deltaTime);
    }

    // --- INPUT SYSTEM CALLBACKS ---

    public void HandleMovement(InputAction.CallbackContext value)
    {
        moveInput = value.ReadValue<Vector2>();
    }

    public void HandleLook(InputAction.CallbackContext value)
    {
        lookInput = value.ReadValue<Vector2>();
    }

    public void HandleSprint(InputAction.CallbackContext value)
    {
        if (value.started) isSprinting = true;
        if (value.canceled) isSprinting = false;
    }

    public void HandleCrouch(InputAction.CallbackContext value)
    {
        if (value.started) wantsToCrouch = true;
        if (value.canceled) wantsToCrouch = false;
    }

    public void HandleToggleAutoRun(InputAction.CallbackContext value)
    {
        if (value.started) isAutoRun = !isAutoRun;
    }

    public void HandleJump(InputAction.CallbackContext value)
    {
        if (!value.started) return;

        bool isGroundedOrCoyote = coyoteTimeCounter > 0f;
        bool hasDoubleJumps = jumpCount < maxJumps;
        
        // Prevent jumping if there is a ceiling immediately above (stops glitching through floors)
        // We use a slightly smaller ray than full height to allow jumping in loose tunnels
        bool underCeiling = Physics.Raycast(transform.position, Vector3.up, standingHeight * 0.9f, obstacleLayer);

        if ((isGroundedOrCoyote || hasDoubleJumps) && !underCeiling)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;
            coyoteTimeCounter = 0f; 
        }
    }
}