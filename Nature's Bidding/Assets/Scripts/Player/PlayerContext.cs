using UnityEngine;

[System.Serializable]
public class PlayerContext
{
    [Header("World Context")]
    public bool isGrounded;
    public LayerMask isGroundLayers;

    [Header("References")]
    public Animator anim;
    public Rigidbody rb;
    public Transform orientation;
    public Camera cam;

    [Header("Speed")]
    public float moveSpeed;
    public float sprintSpeed;
    public float acceleration;
    public float turnSpeed;
    public float groundDrag;
    public float airDrag;
    public float airControlMultiplier;
    public Vector3 forceToAdd;
    public ForceMode forceMode;

    [Header("Jump Values")]
    public float jumpImpulse;
    public float jumpHeldForce;
    public float jumpHeldAllowedTime;

    [Header("Input")]
    public Vector3 moveInput;
    public bool sprintPressed;
    public bool dashPressed;
    public bool jumpPressed;
    public bool attackPressed;
    public bool parryPressed;
}
