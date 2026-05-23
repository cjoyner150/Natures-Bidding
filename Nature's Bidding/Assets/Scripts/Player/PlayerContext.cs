using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerContext
{
    public bool allowInputs = true;

    [Header("Stats")]
    public BasePlayerStats BaseStats;
    public List<StatusEffectorSO> statusEffectsOnStart;
    public Stats playerStats;

    [Header("World Context")]
    public bool isGrounded;
    public LayerMask isGroundLayers;

    [Header("References")]
    public Animator anim;
    public Rigidbody rb;
    public Transform modelHolder;
    public Transform orientation;
    public Camera cam;
    public PlayerAttackManager playerAttackManager;
    public PlayerHealth playerHealth;

    [Header("Speed")]

    [Header("State Desired Speeds")]
    public float walkSpeed;
    public float sprintSpeed;
    public float attackSpeed;
    public float dashSpeed;
    public float airSpeed;
    public float knockbackSpeed;

    [Header("Speed Options")]
    public float currentMaxSpeed;
    public float desiredMaxSpeed;
    public float momentumLerpSpeed;
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
    public float extraGravityMultiplier;

    [Header("Attacks")]
    public float attackTime;
    public float jumpAttackTime;
    public float fallAttackTime;
    public float attackCD;
    public float attackCDTimer;
    public bool attackOnCooldown => attackCDTimer > 0;
    public float attackResponseForce;
    public bool hitResponse;
    public int attackActiveDelay;

    [Header("Parry")]
    public bool parryResponse;
    public float parryCDTimer;
    public bool parryOnCooldown => parryCDTimer > 0;
    public int parryWarmUpDelay;

    [Header("Stun")]
    public bool shouldStunSelf;
    public bool isStunned;
    public float stunTime;
    public float stunRecoveryTimer;

    [Header("Dash")]
    public float dashTime;
    public float dashCDTimer;
    public bool dashOnCooldown => dashCDTimer > 0;
    public float dashRotateMultiplier;


    [Header("Knockback")]
    public float knockbackTime;
    public Vector3 lastHitFromPosition;
    public bool shouldTakeKnockback;

    [Header("Input")]
    public Vector3 moveInput;
    public bool moveInputIsSprint;
    public bool dashPressed;
    public bool jumpPressed;
    public bool attackPressed;
    public bool parryPressed;
}