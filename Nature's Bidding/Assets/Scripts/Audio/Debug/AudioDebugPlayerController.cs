using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class AudioDebugPlayerController : MonoBehaviour
{
    public Camera followCamera;
    public float moveSpeed = 7f;
    public float jumpSpeed = 5f;
    public Vector3 cameraOffset = new Vector3(0f, 20f, -18f);
    public Vector3 cameraLookOffset = new Vector3(0f, 0f, 4f);

    private Rigidbody body;
    private Vector3 spawnPosition;
    private Vector2 movement;
    private bool jumpRequested;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        spawnPosition = body.position;
        if (followCamera == null)
            followCamera = Camera.main;
    }

    private void Update()
    {
        movement = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            jumpRequested |= keyboard.spaceKey.wasPressedThisFrame;
            if (keyboard.rKey.wasPressedThisFrame)
                ResetPosition();
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > movement.sqrMagnitude)
                movement = stick;
            jumpRequested |= gamepad.buttonSouth.wasPressedThisFrame;
        }

        movement = Vector2.ClampMagnitude(movement, 1f);
        if (body.position.y < -5f)
            ResetPosition();
    }

    private void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = movement.x * moveSpeed;
        velocity.z = movement.y * moveSpeed;
        if (jumpRequested && Physics.SphereCast(body.position - Vector3.up * 0.65f,
                0.25f, Vector3.down, out _, 0.3f, 1, QueryTriggerInteraction.Ignore))
            velocity.y = jumpSpeed;
        jumpRequested = false;
        body.linearVelocity = velocity;
    }

    private void LateUpdate()
    {
        if (followCamera == null)
            return;
        Vector3 target = transform.position;
        followCamera.transform.position = target + cameraOffset;
        followCamera.transform.rotation = Quaternion.LookRotation(
            target + cameraLookOffset - followCamera.transform.position, Vector3.up);
    }

    public void ResetPosition()
    {
        body.position = spawnPosition;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }
}
