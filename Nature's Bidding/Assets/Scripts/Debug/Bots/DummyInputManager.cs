using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class DummyInputManager : PlayerInputManager
{
    [Header("Config")]
    [SerializeField] private DummyState behaviourState;
    [SerializeField] private bool resetPosition;
    [SerializeField] private float idleTransitionTime;

    [Header("Debug (Do Not Change)")]
    [SerializeField] private float idleTimer = 0;
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private bool shouldStartInput;

    Dictionary<DummyState, IInputStrategy> inputStrategyLookup;

    private IInputStrategy dummyInput;

    public override void InitializePlayer(PlayerContext context)
    {
        base.InitializePlayer(context);

        inputStrategyLookup = new Dictionary<DummyState, IInputStrategy>
        {
            { DummyState.idle, new IdleInputStrategy(ctx) },
            { DummyState.walking, new WalkInputStrategy(ctx) },
            { DummyState.attacking, new AttackInputStrategy(ctx)},


        };

        dummyInput = inputStrategyLookup[behaviourState];

        idleTimer = idleTransitionTime;
        startPosition = ctx.rb.position;
        shouldStartInput = false;

        allowInputs = true;
        ctx.allowInputs = true;
    }

    protected override void PlayerInput()
    {
        if (dummyInput.IsRunning) dummyInput.Tick(Time.deltaTime);

        if (root.Leaf() is Idle && !dummyInput.IsRunning)
        {
            HandleIdleTick();
        }
        else idleTimer = idleTransitionTime;

        if (!allowInputs || !ctx.allowInputs)
        {
            ctx.moveInput = Vector3.zero;
            ctx.jumpPressed = false;
            ctx.dashPressed = false;
            ctx.attackPressed = false;
            ctx.parryPressed = false;
            return;
        }

        if (shouldStartInput && !dummyInput.IsRunning)
        {
            dummyInput.Start();

            dummyInput.OnComplete += () =>
            {
                if (resetPosition)
                {
                    ctx.rb.linearVelocity = Vector3.zero;
                    ctx.rb.angularVelocity = Vector3.zero;
                    ctx.rb.position = startPosition;

                    Physics.SyncTransforms();
                }
            };

            shouldStartInput = false;
        }
    }

    private void HandleIdleTick()
    {
        idleTimer -= Time.deltaTime;
        
        if (idleTimer <= 0)
        {
            shouldStartInput = true;
            idleTimer = idleTransitionTime;
        }
    }

    protected override void SetOwnedPlayerLayers()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>();

        foreach (Transform t in transforms) t.gameObject.layer = LayerMask.NameToLayer("Player");
    }
}
