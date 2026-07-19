using HSM;
using Unity.Netcode.Components;
using UnityEngine;
public class AirDash : State
{
    private readonly PlayerContext ctx;
    
    private Vector3 momentumDirection;
    private bool exitDash;
    private bool teleport;
    private float dashTimer;

    public AirDash(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;

        teleport = ctx.teleportOnDash;

        if (teleport)
        {
            Teleport();
            NetworkVisualEffectManager.SpawnTeleportEffectsOnPlayer?.Invoke(ctx.playerHealth.OwnerClientId);
            return;
        }

        ctx.anim.SetBool("AirDashing", true);
        NetworkVisualEffectManager.SpawnDashEffectsOnPlayer?.Invoke(ctx.playerHealth.OwnerClientId);

        ctx.desiredMaxSpeed = ctx.dashSpeed * ctx.playerStats.DashDistance;

        dashTimer = ctx.dashTime;
        exitDash = false;

        ctx.rb.useGravity = false;
    }

    protected override void OnExit()
    {
        ctx.anim.SetBool("AirDashing", false);

        ctx.dashCDTimer = ctx.playerStats.DashCooldown;
        ctx.rb.useGravity = true;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.rb.linearVelocity = momentumDirection * ctx.desiredMaxSpeed;

        HandleRotation(deltaTime);

        dashTimer -= deltaTime;
        if (dashTimer <= 0) exitDash = true;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, momentumDirection, ctx.turnSpeed * deltaTime * ctx.dashRotateMultiplier);
    }

    protected void Teleport()
    {
        ctx.modelHolder.forward = momentumDirection;

        var boxCenter = ctx.rb.transform.position + (ctx.rb.transform.up * 1.4f * ctx.playerStats.Size);
        var halfExtents = new Vector3(.5f, 1f, .5f) * ctx.playerStats.Size;
        var direction = ctx.modelHolder.forward;
        var orientation = Quaternion.identity;
        var maxDistance = ctx.teleportDistance * ctx.playerStats.DashDistance;

        bool didHit = Physics.BoxCast(boxCenter, halfExtents, direction, out RaycastHit hit, orientation, maxDistance, ctx.teleportBlockingLayer);

        if (ctx.debugTeleport)
        {
            DebugDrawBox(boxCenter, halfExtents, orientation, Color.green, 3f);
            var endCenter = boxCenter + direction * (didHit ? hit.distance : maxDistance);
            DebugDrawBox(endCenter, halfExtents, orientation, didHit ? Color.red : Color.yellow, 3f);
            Debug.DrawLine(boxCenter, endCenter, Color.cyan, 3f);
        }

        Vector3 teleportPosition;

        if (hit.collider != null)
        {
            var difference = new Vector3(hit.point.x, ctx.rb.transform.position.y, hit.point.z) - ctx.rb.transform.position;
            teleportPosition = ctx.rb.position + (difference * .8f);
        }
        else
        {
            teleportPosition = ctx.rb.transform.position + (direction * maxDistance);
        }

        ctx.rb.linearVelocity = Vector3.zero;
        ctx.rb.angularVelocity = Vector3.zero;

        ctx.rb.position = teleportPosition;

        var networkTransform = ctx.rb.gameObject.GetComponent<NetworkTransform>();
        if (networkTransform != null)
            networkTransform.Teleport(teleportPosition, ctx.modelHolder.rotation, ctx.rb.transform.localScale);

        Physics.SyncTransforms();

        exitDash = true;
    }

    private void DebugDrawBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, Color color, float duration = 0f)
    {
        Vector3[] c = new Vector3[8];
        c[0] = center + orientation * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        c[1] = center + orientation * new Vector3( halfExtents.x, -halfExtents.y, -halfExtents.z);
        c[2] = center + orientation * new Vector3( halfExtents.x, -halfExtents.y,  halfExtents.z);
        c[3] = center + orientation * new Vector3(-halfExtents.x, -halfExtents.y,  halfExtents.z);
        c[4] = center + orientation * new Vector3(-halfExtents.x,  halfExtents.y, -halfExtents.z);
        c[5] = center + orientation * new Vector3( halfExtents.x,  halfExtents.y, -halfExtents.z);
        c[6] = center + orientation * new Vector3( halfExtents.x,  halfExtents.y,  halfExtents.z);
        c[7] = center + orientation * new Vector3(-halfExtents.x,  halfExtents.y,  halfExtents.z);

        Debug.DrawLine(c[0], c[1], color, duration);
        Debug.DrawLine(c[1], c[2], color, duration);
        Debug.DrawLine(c[2], c[3], color, duration);
        Debug.DrawLine(c[3], c[0], color, duration);
        Debug.DrawLine(c[4], c[5], color, duration);
        Debug.DrawLine(c[5], c[6], color, duration);
        Debug.DrawLine(c[6], c[7], color, duration);
        Debug.DrawLine(c[7], c[4], color, duration);
        Debug.DrawLine(c[0], c[4], color, duration);
        Debug.DrawLine(c[1], c[5], color, duration);
        Debug.DrawLine(c[2], c[6], color, duration);
        Debug.DrawLine(c[3], c[7], color, duration);
    }

    protected override State GetTransition() 
    {
        if (exitDash) return GetParentOfType<Airborne>().fall;
        else return null;
    }

}