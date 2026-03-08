using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using HSM;
using System.Linq;
using Unity.Netcode;

public class PlayerInputManager : NetworkBehaviour
{
    public PlayerContext ctx;

    [Header("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform orientation;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Camera cam;

    [Header("Player Controls")]
    private PlayerControls controls;
    private InputAction move;
    private InputAction sprint;
    private InputAction jump;
    private InputAction parry;
    private InputAction attack;

    private bool allowInputs = false;
    public bool allowSprint = true;
    public bool allowJump = true;
    public bool allowAttack = true;
    public bool allowParry = true;

    private StateMachine sm;
    private State root;

    private void Awake()
    {
        if (!IsOwner) return;

        controls = new PlayerControls();
        move = controls.PlayerGameplay.Move;
        sprint = controls.PlayerGameplay.Sprint;
        //jump = controls.PlayerGameplay.Jump;
        //attack = controls.PlayerGameplay.LightAttack;
        //parry = controls.PlayerGameplay.Parry;

        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        orientation = Instantiate(new GameObject(), transform).transform;
        orientation.rotation = transform.rotation;
        orientation.position = transform.position;

        cam = Camera.main;

        root = new PlayerRoot(null, ctx);
        var builder = new StateMachineBuilder(root);

        sm = builder.Build();

    }

    private void OnEnable()
    {
        move.Enable();
        sprint.Enable();
        jump.Enable();
        attack.Enable();
        parry.Enable();
    }

    private void OnDisable()
    {
        move.Disable();
        sprint.Disable();
        jump.Disable();
        attack.Disable();
        parry.Disable();

    }

    void Update()
    {
        if (!IsOwner) return;

        HandleOrientation();
        ctx.isGrounded = CheckGrounded();

        PlayerInput();

        sm.Tick(Time.deltaTime);

        // DebugCurrentState();
    }

    private void HandleOrientation()
    {
        Vector3 cameraRelativeOrientation = transform.position - cam.transform.position;
        cameraRelativeOrientation.y = 0;
        cameraRelativeOrientation = cameraRelativeOrientation.normalized;

        orientation.forward = cameraRelativeOrientation;
    }

    private bool CheckGrounded()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, .2f, ctx.isGroundLayers);
        return colliders.Length > 0;
    }

    // Debug ONLY shows the grounded check visibly on selecting the player
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, .2f);
    }

    public void DebugCurrentState() => Debug.Log(ActivePathString(root.Leaf()));

    static string ActivePathString(State s)
    {
        return string.Join(" > ", s.GetActivePath().AsEnumerable().Reverse().Select(n => n.GetType().Name));
    }

    void PlayerInput()
    {

        if (!allowInputs || !IsOwner) return;

        Vector2 moveInput = move.ReadValue<Vector2>();
        ctx.sprintPressed = allowSprint ? sprint.IsPressed() : false;

        Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;
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
