using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MechAI : BaseEnemyCharacter
{
    public override Vector2 Orientation
    {
        get { return Mathf.Sign(modelHolder.right.x) * Vector2.right; }
        // use base setter
        set { base.Orientation = value; }
    }
    public float moveSpeed = 1;
    public float faceAngle = 15;
    public float walkingLevelHeight = 4.5f;
    public float[] activeMargin = new float[]{0,0}; // left right
    public Transform modelHolder;
    private AbstractWeapon weapon;
    public Animator animator;

    protected override void BuildStates()
    {
        base.BuildStates();
        stateDict["Idle"] = new MinimalIdleState
        (
            this,
            duration: 5f,
            delta: 1.25f
        );

        stateDict["Pursue"] = new MechPursueState
        (
            this,
            durationRaw: 5f,
            delta: 1.25f,
            moveSpeed: moveSpeed
        );

        stateDict["Idle"].transition["timeUp"] = stateDict["Pursue"];
        stateDict["Pursue"].transition["timeUp"] = stateDict["Idle"];
        stateDict["Pursue"].transition["inPosition"] = stateDict["Idle"];
        defaultState = stateDict["Idle"];

        stateDict["Pursue"].stageActions[StateStage.UPDATE] += AutoAdjustAnimSpeed;
        stateDict["Idle"].stageActions[StateStage.ENTER] += () => 
        {
            if (animator == null) { return; }
            animator.SetTrigger("ToIdle");
            animator.speed = 1;
        };
        stateDict["Pursue"].stageActions[StateStage.ENTER] += () => 
        {
            if (animator == null) { return; }
            animator.SetTrigger("ToWalk");
        };
    }

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        weapon = weaponHolder.GetComponentInChildren<AbstractWeapon>();
    }

    protected override void Update()
    {
        base.Update();

        if (DriveControlVector != Vector2.zero)
        {
            Vector2 driveTarget = Position + DriveControlVector;
            driveTarget = driveTarget.ClampRectangular(
                new Vector2(IOManager.Instance.worldBounds[2]+activeMargin[0], IOManager.Instance.worldBounds[1]),
                new Vector2(IOManager.Instance.worldBounds[3]-activeMargin[1], IOManager.Instance.worldBounds[0]));
            driveControlVector = driveTarget - Position;
        }
        overrideDriveControlVector = false;

        Vector2 toTarget = Vector2.zero;
        Vector2 interceptTarget = Vector2.zero;
        if (CurrentTarget != null)
        {
            toTarget = CurrentTarget.Position - Position;
            interceptTarget = weapon != null? weapon.AimToTarget(CurrentTarget.Position, CurrentTarget.Velocity, Velocity): toTarget;
        }
        if (!overrideFireControlVector) { FireControlVector = interceptTarget; }
        overrideFireControlVector = false;
    }

    void FixedUpdate()
    {
        Vector3 faceDirection; // rotate driveControlVector towards vector3.forward by faceAngle, set this value to faceDirection
        if (DriveControlVector == Vector2.zero) { faceDirection = -Vector3.forward; }
        else { faceDirection = Vector3.RotateTowards((Vector3)DriveControlVector, -Vector3.forward, faceAngle*Mathf.Deg2Rad, 1f); }
   
        if (Position.y < CharacterSize + walkingLevelHeight) { Velocity = new Vector2(Velocity.x, Mathf.Max(0, Velocity.y)); CharacterRB2D.gravityScale = 0; }
        else { CharacterRB2D.gravityScale = 1; }
        Position = new Vector2(Position.x, Mathf.Max(Position.y, CharacterSize + walkingLevelHeight));
        Position = Vector3.MoveTowards(Position, new Vector3(Position.x, CharacterSize + walkingLevelHeight, transform.position.z), 2*Constants.REFERENCE_MOVE_SPEED*Time.fixedDeltaTime);
        modelHolder.right = Vector3.RotateTowards(modelHolder.right, faceDirection, Mathf.PI * 0.5f * Time.fixedDeltaTime, 1f);

        if (DriveControlVector == Vector2.zero) { return; }
        float? speedPct = bonusStats.moveSpeedPct;
        float weightedMoveSpeed = moveSpeed.ApplyPct(speedPct);
        Velocity = Vector2.MoveTowards(Velocity, weightedMoveSpeed * DriveControlVector.normalized, 5f * weightedMoveSpeed * Time.fixedDeltaTime);
    }

    private void AutoAdjustAnimSpeed()
    {
        float dot = Mathf.Abs(Vector3.Dot(modelHolder.right, Vector3.right));
        dot = Mathf.Clamp(dot, Mathf.Cos(faceAngle*Mathf.Deg2Rad), 1f);
        animator.speed = Mathf.Abs(Velocity.x / dot);
    }
}

public class MechPursueState: CharacterBaseState
{
    private Vector2 controlVector;
    private float moveSpeed;
    public MechPursueState(GameCharacter baseCharacter, float durationRaw = 0, float delta = 0, float moveSpeed=Constants.REFERENCE_MOVE_SPEED) : base(baseCharacter, durationRaw, delta)
    {
        this.name = "MechPursueState";
        this.moveSpeed = moveSpeed;
    }
    public override void Enter()
    {
        base.Enter();
        if (baseCharacter.CurrentTarget == null) { controlVector = Mathf.Sign(UnityEngine.Random.value-0.5f) * Vector2.right; }
        else
        {
            Vector2 targetFuturePosition = baseCharacter.CurrentTarget.Position + maxDuration * baseCharacter.CurrentTarget.Velocity;
            Vector2 ifMoveLeftPosition = baseCharacter.Position + moveSpeed * maxDuration * Vector2.left;
            Vector2 ifMoveRightPosition = baseCharacter.Position + moveSpeed * maxDuration * Vector2.right;
            if (targetFuturePosition.HorizontalDististance(ifMoveLeftPosition) < targetFuturePosition.HorizontalDististance(ifMoveRightPosition))
            {
                controlVector = ifMoveLeftPosition - baseCharacter.Position;
            }
            else
            {
                controlVector = ifMoveRightPosition - baseCharacter.Position;
            }
        }
        if (!baseCharacter.overrideDriveControlVector) { baseCharacter.DriveControlVector = controlVector; }   
        baseCharacter.overrideDriveControlVector = false;
    }
    public override void Update()
    {
        base.Update();
        if (transition.ContainsKey("inPosition") &&
            transition["inPosition"] != null &&
            baseCharacter.DriveControlVector.sqrMagnitude < Constants.TOLERANCE)
        {
            nextState = transition["inPosition"];
            stage = StateStage.EXIT; 
        }

        if (!baseCharacter.overrideDriveControlVector) { baseCharacter.DriveControlVector = controlVector; }   
        baseCharacter.overrideDriveControlVector = false;
    }
}
