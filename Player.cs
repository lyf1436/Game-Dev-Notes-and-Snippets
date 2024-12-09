using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ace.BuffSystem;

public class Player : GameCharacter
{
    //kinematics
    [Header("Kinematics Parameters")]
    public float rotateSpeed = 2.0f;
    public float moveSpeed = 2.0f;
    public float boostSpeed = 4.0f;
    public float boostRotateSpeed = 270f;
    public float brakeRotateSpeed = 480;
    public float propulsionForce = 15;
    public float wingLoadDamp = 5;

    //control
    [Header("Control Related Parameters")]
    public float damageDeduction;
    public float gasRegainRate;
    public float gasConsumptionRate;
    public float angerRegainRate = 1;
    [SerializeField] private float invincibleDuration = 0.2f;

    //gamecharacter
    [Header("GameCharacter Parameters")]
    public ObservableValue<float> gas = new ObservableValue<float>();
    public float Gas
    {
        get { return gas.Value * 100; }
        set { gas.Value = Mathf.Clamp01(value / 100f); }
    }
    public ObservableValue<float> rage = new ObservableValue<float>();
    public float Rage
    {
        get { return rage.Value * 100; }
        set { rage.Value = Mathf.Clamp01(value / 100f); }    
    }
    public ObservableValue<float> health = new ObservableValue<float>();
    public override float Health
    {
        get { return health.Value * MaxHealth; }
        set { health.Value = Mathf.Clamp01(value / MaxHealth); }
    }
    [SerializeField]
    private float maxHealth;
    public override float MaxHealth
    {
        get { return maxHealth; }
        set { maxHealth = value; }
    }
    public ObservableValue<float> speed;


    [Header("Player Related Objects")]
    [SerializeField]
    public PlayerCfg playerCFG;

    [SerializeField]
    public BuffHolder buffHolder;
    private bool invincible;
    //------------------------player dynamics------------------

    protected override void BuildStates()
    {
        base.BuildStates();
        stateDict["Normal"] = new PhysicalFlightState
        (
            this, 
            targetSpeed: moveSpeed,
            angularSpeed: rotateSpeed,
            propulsion: propulsionForce,
            wingDrag: wingLoadDamp
        ) { name = "Normal" };

        stateDict["Boost"] = new PhysicalFlightState
        (
            this,
            targetSpeed: boostSpeed,
            angularSpeed: boostRotateSpeed,
            propulsion: propulsionForce * 2f,
            wingDrag: wingLoadDamp
        ) { name = "Boost" };

        stateDict["Brake"] = new PhysicalFlightState
        (
            this,
            targetSpeed: 0,
            angularSpeed: brakeRotateSpeed,
            propulsion: 0,
            wingDrag: wingLoadDamp,
            gravityScale: 0
        ) { name = "Brake" };

        defaultState = stateDict["Normal"];

        stateDict["Normal"].stageActions[StateStage.ENTER] += () => { CharacterManager.Instance.playerAngularSpeed = rotateSpeed; };
        stateDict["Boost"].stageActions[StateStage.ENTER] += () => { CharacterManager.Instance.playerAngularSpeed = boostRotateSpeed; };
        stateDict["Brake"].stageActions[StateStage.ENTER] += () => { CharacterManager.Instance.playerAngularSpeed = brakeRotateSpeed; };

        stateDict["Normal"].stageActions[StateStage.UPDATE] += () => { Gas += gasRegainRate * Time.deltaTime; };
        stateDict["Boost"].stageActions[StateStage.UPDATE] += () => { Gas -= gasConsumptionRate * Time.deltaTime; };
        stateDict["Brake"].stageActions[StateStage.UPDATE] += () => { Gas -= gasConsumptionRate * Time.deltaTime; };
    }
    
    protected override void Awake()
    {
        base.Awake();

        Health = MaxHealth;
        Gas = 100;
        Rage = 50f;
        transform.rotation = Quaternion.identity;
    }
    void Start()
    {
        buffHolder = new BuffHolder(this);
        StartCoroutine(IEAddAnger());
    }
    IEnumerator IEAddAnger()
    {
        while (true)
        {
            if (Rage >= 100) { yield return new WaitUntil(() => Rage <= 0); }
            Rage += angerRegainRate * Time.deltaTime;
            yield return null;
        }
    }
    void FixedUpdate()
    {
        ((IPhysicalState)stateMachine).ProcessPhysics();
        Position = Position.ClampRectangular(new Vector2(IOManager.Instance.worldBounds[2], IOManager.Instance.worldBounds[1]), new Vector2(IOManager.Instance.worldBounds[3], IOManager.Instance.worldBounds[0]));
        speed.Value = Velocity.magnitude;
    }
    protected override void Update()
    {
        base.Update();
        Debug.DrawRay(Position, 2 * Orientation, Color.blue);

        if (Gas <= 0)
        {
            stateMachine = stateMachine.ForceTransition(stateDict["Normal"]);
        }
        else
        {
            if (IOManager.Instance.boostInputState == InputState.Pressed)
            {
                stateMachine = stateMachine.ForceTransition(stateDict["Boost"]);
            }
            if (IOManager.Instance.brakeInputState == InputState.Pressed)
            {
                stateMachine = stateMachine.ForceTransition(stateDict["Brake"]);
            }
            if (IOManager.Instance.boostInputState == InputState.Released || IOManager.Instance.brakeInputState == InputState.Released)
            {
                stateMachine = stateMachine.ForceTransition(stateDict["Normal"]);
            }
        }

        if (IOManager.Instance.firePrimaryState != 0 && FireControlVector!=Vector2.zero)
        {
            playerCFG.PrimaryWeapon?.TriggerWeapon(IOManager.Instance.firePrimaryState);
        }
        if(IOManager.Instance.fireSecondaryState != 0 && FireControlVector!=Vector2.zero)
        {
            playerCFG.SecondaryWeapon?.TriggerWeapon(IOManager.Instance.fireSecondaryState);
        }

        CharacterManager.Instance.playerControlVec = DriveControlVector;
        CharacterManager.Instance.playerVelocity = Velocity;
        CharacterManager.Instance.playerPosition = Position;
        CharacterManager.Instance.playerOrientation = CharacterRB2D.rotation.Tangent();

        if (Input.GetKeyDown(KeyCode.K))
        {
            // kill player
            TakeDamage(new TakeDamageInput(amount: 33, damageType: DamageType.Basic));
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            Health += 25f;
        }
    }
    private IEnumerator Invincible()
    {
        invincible = true;
        yield return new WaitForSeconds(invincibleDuration);
        invincible = false;
    }
    public override TakeDamageReturn TakeDamage(TakeDamageInput tdi=default)
    {
        if (!gameObject.activeSelf) { return new TakeDamageReturn(false, false); }
        
        if (tdi.damageType != DamageType.Burn && invincible) { return new TakeDamageReturn(false, false); }
        if (!invincible) { StartCoroutine(Invincible()); }

        Rage += 5f;
        Health -= damageDeduction * tdi.amount;
        CharacterTakeDamageEvent?.Invoke(this);

        StatsManager.Instance.totalDamageReceived += damageDeduction * tdi.amount;
        if (Health <= 0)
        {
            SelfDestroy();
            return new TakeDamageReturn(true, true, gameObject);
        }
        else
        {
            return new TakeDamageReturn(false, true, gameObject);
        }
    }
    public override void SelfDestroy()
    {
        base.SelfDestroy();
        Debug.Log("Player death");
        StopAllCoroutines();
        CharacterManager.Instance.PlayerDown();
    }

    [ContextMenu("Calculate Kinematics")]
    void ShowDamp()
    {
        if (CharacterRB2D == null) { CharacterRB2D = GetComponent<Rigidbody2D>(); }
        Debug.Log("50%: " + (-Mathf.Log(0.5f) * CharacterRB2D.mass / CharacterRB2D.drag).ToString());
        Debug.Log("10%: " + (-Mathf.Log(0.1f) * CharacterRB2D.mass / CharacterRB2D.drag).ToString());
        Debug.Log("Max Speed Achieveable: " + (propulsionForce*2f / CharacterRB2D.drag * CharacterRB2D.mass).ToString());
    }
    // public override void DisableCharacter()
    // {
    //     Debug.LogWarning("This is not preferred");
    //     gameObject.SetActive(false);
    // }
}
