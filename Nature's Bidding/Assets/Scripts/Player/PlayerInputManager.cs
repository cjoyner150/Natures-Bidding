using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using HSM;
using System.Linq;
using Unity.Netcode;

public class PlayerInputManager : MonoBehaviour
{
    private PlayerContext ctx;

    [Header("Player Controls")]
    private PlayerControls controls;
    private InputAction move;
    private InputAction sprint;
    private InputAction dash;
    private InputAction jump;
    private InputAction parry;
    private InputAction attack;

    private bool allowInputs = false;
    public bool allowSprint = true;
    public bool allowDash = true;
    public bool allowJump = true;
    public bool allowAttack = true;
    public bool allowParry = true;

    private StateMachine sm;
    private State root;

    public void InitializePlayer(PlayerContext context)
    {
        ctx = context;

        controls = new PlayerControls();
        move = controls.PlayerGameplay.Move;
        sprint = controls.PlayerGameplay.Sprint;
        dash = controls.PlayerGameplay.Dash;
        jump = controls.PlayerGameplay.Jump;
        attack = controls.PlayerGameplay.Attack;
        parry = controls.PlayerGameplay.Parry;

        ctx.orientation = Instantiate(new GameObject(), transform).transform;
        ctx.orientation.rotation = transform.rotation;
        ctx.orientation.position = transform.position;

        ctx.cam = Camera.main;

        root = new PlayerRoot(null, ctx);
        var builder = new StateMachineBuilder(root);

        sm = builder.Build();

        move.Enable();
        sprint.Enable();
        dash.Enable();
        jump.Enable();
        attack.Enable();
        parry.Enable();

        allowInputs = true;
    }

    private void OnDestroy()
    {
        move.Disable();
        sprint.Disable();
        dash.Disable();
        jump.Disable();
        attack.Disable();
        parry.Disable();
    }

    void Update()
    {

        HandleOrientation();
        ctx.isGrounded = CheckGrounded();

        PlayerInput();

        sm.Tick(Time.deltaTime);

        DebugCurrentState();
    }

    private void FixedUpdate()
    {
        HandlePhysicsMove();
    }

    private void HandlePhysicsMove()
    {
        ctx.rb.AddForce(ctx.forceToAdd * Time.fixedDeltaTime, ctx.forceMode);
    }

    private void HandleOrientation()
    {
        Vector3 cameraRelativeOrientation = ctx.cam.transform.forward;
        cameraRelativeOrientation.y = 0;
        cameraRelativeOrientation = cameraRelativeOrientation.normalized;

        ctx.orientation.forward = cameraRelativeOrientation;
    }

    private bool CheckGrounded()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position + (transform.up * .125f), .2f, ctx.isGroundLayers);
        return colliders.Length > 0;
    }

    public void DebugCurrentState() => Debug.Log(ActivePathString(root.Leaf()));

    static string ActivePathString(State s)
    {
        return string.Join(" > ", s.GetActivePath().AsEnumerable().Reverse().Select(n => n.GetType().Name));
    }

    void PlayerInput()
    {
        if (!allowInputs) return;

        Vector2 moveInput = move.ReadValue<Vector2>();
        ctx.sprintPressed = allowSprint ? sprint.IsPressed() : false;
        ctx.jumpPressed = allowJump ? jump.IsPressed() : false;
        ctx.dashPressed = allowDash ? dash.IsPressed() : false;
        ctx.attackPressed = allowAttack ? attack.IsPressed() : false;
        ctx.parryPressed = allowParry ? parry.IsPressed() : false;

        Vector3 moveDirection = ctx.orientation.forward * moveInput.y + ctx.orientation.right * moveInput.x;
        moveDirection.Normalize();

        ctx.moveInput = moveDirection;

    }

    void OnPauseGame()
    {
        allowInputs = false;
    }

    async void OnUnpauseGame()
    {
        await UniTask.Delay(500);
        allowInputs = true;
    }
}
