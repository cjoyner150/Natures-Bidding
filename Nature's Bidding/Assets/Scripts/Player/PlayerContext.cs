using UnityEngine;

[System.Serializable]
public class PlayerContext
{
    [Header("World Context")]
    public bool isGrounded;
    public LayerMask isGroundLayers;

    [Header("Speeds")]
    public float moveSpeed;
    public float acceleration;

    [Header("Input")]
    public Vector3 moveInput;
    public bool sprintPressed;
}
