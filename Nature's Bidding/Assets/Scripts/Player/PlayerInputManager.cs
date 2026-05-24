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
    private InputAction move;
    private InputAction sprint;
    private InputAction dash;
    private InputAction jump;
    private InputAction parry;
    private InputAction attack;
    private InputAction pause;

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
        move = controls.PlayerGameplay.Move;
        sprint = controls.PlayerGameplay.Sprint;
        dash = controls.PlayerGameplay.Dash;
        jump = controls.PlayerGameplay.Jump;
        attack = controls.PlayerGameplay.Attack;
        parry = controls.PlayerGameplay.Parry;
        pause = controls.PlayerGameplay.Pause;

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

        pause.performed += OnPausePressed;
        PlayerPauseManager.Instance.OnPaused += OnPaused;
        PlayerPauseManager.Instance.OnResumed += OnResumed;

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
        controls?.Disable();
        move?.Disable();
        sprint?.Disable();
        dash?.Disable();
        jump?.Disable();
        attack?.Disable();
        parry?.Disable();
        pause?.Disable();

        if (pause != null)
            pause.performed -= OnPausePressed;

        if (PlayerPauseManager.HasInstance)
        {
            PlayerPauseManager.Instance.OnPaused -= OnPaused;
            PlayerPauseManager.Instance.OnResumed -= OnResumed;
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
        return string.Join(" > ", s.GetActivePath().AsEnumerable().Reverse().Select(n => n.GetType().Name));
    }

    void PlayerInput()
    {
        if (!allowInputs || !ctx.allowInputs) return;

        Vector2 moveInput = move.ReadValue<Vector2>();
        ctx.jumpPressed = allowJump && jump.IsPressed();
        ctx.dashPressed = allowDash && dash.IsPressed();
        ctx.attackPressed = allowAttack && attack.IsPressed();
        ctx.parryPressed = allowParry && parry.IsPressed();

        ctx.moveInputIsSprint = allowSprint && (Mathf.Abs(moveInput.x) > .4f || Mathf.Abs(moveInput.y) > .4f);

        Vector3 moveDirection = (ctx.orientation.forward * moveInput.y + ctx.orientation.right * moveInput.x).normalized;

        ctx.moveInput = moveDirection;

    }

    void OnPausePressed(InputAction.CallbackContext callback)
    {
        if (allowPause)
        {
            PlayerPauseManager.Instance.OnPausePressed?.Invoke();
        }
    }

    void OnPaused()
    {
        allowInputs = !PlayerPauseManager.Instance.Paused;
    }

    void OnResumed()
    {
        allowInputs = !PlayerPauseManager.Instance.Paused;
    }

}
