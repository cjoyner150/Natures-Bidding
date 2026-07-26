using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using HSM;
using System.Linq;
using UnityUtils;

public class PlayerInputManager : MonoBehaviour
{
    private PlayerContext ctx;

    [Header("Player Controls")]
    private PlayerControls controls;
    private ReversedPlayerControls reversedControls;
    private InputAction move;
    private InputAction sprint;
    private InputAction dash;
    private InputAction jump;
    private InputAction parry;
    private InputAction attack;
    private InputAction pause;
    private InputAction ready;

    private bool allowInputs = false;
    public bool allowSprint = true;
    public bool allowDash = true;
    public bool allowJump = true;
    public bool allowAttack = true;
    public bool allowParry = true;
    public bool allowPause = true;

    private bool paused = false;

    private StateMachine sm;
    private State root;

    public void InitializePlayer(PlayerContext context)
    {
        ctx = context;

        controls = new PlayerControls();
        move ??= controls.PlayerGameplay.Move;
        sprint ??= controls.PlayerGameplay.Sprint;
        dash ??= controls.PlayerGameplay.Dash;
        jump ??= controls.PlayerGameplay.Jump;
        attack ??= controls.PlayerGameplay.Attack;
        parry ??= controls.PlayerGameplay.Parry;
        pause ??= controls.PlayerGameplay.Pause;
        ready ??= controls.PlayerGameplay.Ready;

        ctx.orientation = Instantiate(new GameObject(), transform).transform;
        ctx.orientation.rotation = transform.rotation;
        ctx.orientation.position = transform.position;

        ctx.cam = Camera.main;

        root = new PlayerRoot(null, ctx);
        var builder = new StateMachineBuilder(root);

        sm = builder.Build();

        controls.Enable();
        move.Enable();
        sprint.Enable();
        dash.Enable();
        jump.Enable();
        attack.Enable();
        parry.Enable();
        pause.Enable();
        ready.Enable();

        ready.performed += OnReadyPressed;
        pause.performed += OnPausePressed;
        PlayerPauseManager.OnPaused += OnPaused;
        PlayerPauseManager.OnResumed += OnResumed;

        allowInputs = true;
        
        SetOwnedPlayerLayers();
    }

    private void SetOwnedPlayerLayers()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>();
        
        foreach (Transform t in transforms) t.gameObject.layer = LayerMask.NameToLayer("OwnedPlayer");
    }

    private void OnDestroy()
    {
        reversedControls?.Dispose();
        controls?.Dispose();

        controls?.Disable();
        move?.Disable();
        sprint?.Disable();
        dash?.Disable();
        jump?.Disable();
        attack?.Disable();
        parry?.Disable();
        pause?.Disable();
        ready?.Disable();

        if (pause != null)
            pause.performed -= OnPausePressed;

        if (ready != null)
            ready.performed -= OnReadyPressed;
        

        if (PlayerPauseManager.HasInstance)
        {
            PlayerPauseManager.OnPaused -= OnPaused;
            PlayerPauseManager.OnResumed -= OnResumed;
        }
    }

    void Update()
    {

        HandleOrientation();
        ctx.isGrounded = CheckGrounded();

        PlayerInput();

        sm.Tick(Time.deltaTime);

        //DebugCurrentState();
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
        Collider[] colliders = Physics.OverlapSphere(transform.position + (transform.up * .125f), .25f, ctx.isGroundLayers);
        return colliders.Length > 0;
    }

    public void DebugCurrentState() => Debug.Log(ActivePathString(root.Leaf()));

    static string ActivePathString(State s)
    {
        return string.Join(" > ", s.AncestorPath().Reverse().Select(n => n.GetType().Name));
    }

    void PlayerInput()
    {
        if (!allowInputs || !ctx.allowInputs) 
        {
            ctx.moveInput = Vector3.zero;
            ctx.jumpPressed = false;
            ctx.dashPressed = false;
            ctx.attackPressed = false;
            ctx.parryPressed = false;
            return; 
        }

        Vector2 moveInput = move.ReadValue<Vector2>();
        ctx.jumpPressed = allowJump && jump.IsPressed();
        ctx.dashPressed = allowDash && dash.IsPressed();
        ctx.attackPressed = allowAttack && attack.IsPressed();
        ctx.parryPressed = allowParry && parry.IsPressed();

        ctx.moveInputIsSprint = allowSprint && (Mathf.Abs(moveInput.x) > .4f || Mathf.Abs(moveInput.y) > .4f);

        Vector3 moveDirection = (ctx.orientation.forward * moveInput.y + ctx.orientation.right * moveInput.x).normalized;

        ctx.moveInput = moveDirection;

    }

    public void OnReadyPressed(InputAction.CallbackContext callbackContext)
    {
        LobbyNetworkUI.OnPlayerReadyEvent?.Invoke();
    }

    public void ReverseControls()
    {
        controls?.Dispose();

        reversedControls ??= new();
        reversedControls.Enable();

        move = reversedControls.PlayerGameplay.Move;
        sprint = reversedControls.PlayerGameplay.Sprint;
        dash = reversedControls.PlayerGameplay.Dash;
        jump = reversedControls.PlayerGameplay.Jump;
        attack = reversedControls.PlayerGameplay.Attack;
        parry = reversedControls.PlayerGameplay.Parry;
        pause = reversedControls.PlayerGameplay.Pause;
        ready = reversedControls.PlayerGameplay.Ready;

        move.Enable();
        sprint.Enable();
        dash.Enable();
        jump.Enable();
        attack.Enable();
        parry.Enable();
        pause.Enable();
        ready.Enable();
    }

    public void ResetControls()
    {
        reversedControls?.Dispose();

        controls ??= new();
        controls.Enable();

        move = controls.PlayerGameplay.Move;
        sprint = controls.PlayerGameplay.Sprint;
        dash = controls.PlayerGameplay.Dash;
        jump = controls.PlayerGameplay.Jump;
        attack = controls.PlayerGameplay.Attack;
        parry = controls.PlayerGameplay.Parry;
        pause = controls.PlayerGameplay.Pause;
        ready = controls.PlayerGameplay.Ready;

        move.Enable();
        sprint.Enable();
        dash.Enable();
        jump.Enable();
        attack.Enable();
        parry.Enable();
        pause.Enable();
        ready.Enable();
    }

    public void DisableInput()
    {
        reversedControls?.Disable();
        controls?.Disable();
        move?.Disable();
        sprint?.Disable();
        dash?.Disable();
        jump?.Disable();
        attack?.Disable();
        parry?.Disable();
        pause?.Disable();
        ready?.Disable();
    }

    public void EnableInput()
    {
        reversedControls?.Enable();
        controls?.Enable();
        move?.Enable();
        sprint?.Enable();
        dash?.Enable();
        jump?.Enable();
        attack?.Enable();
        parry?.Enable();
        pause?.Enable();
        ready?.Enable();
    }

    void OnPausePressed(InputAction.CallbackContext callback)
    {
        if (allowPause)
        {
            PlayerPauseManager.OnPausePressed?.Invoke();
        }
    }

    void OnPaused()
    {
        allowInputs = !PlayerPauseManager.Instance.Paused;
        DisableInput();
    }

    void OnResumed()
    {
        allowInputs = !PlayerPauseManager.Instance.Paused;
        EnableInput();
    }

}
