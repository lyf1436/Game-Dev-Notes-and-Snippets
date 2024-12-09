using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum StateStage
{
    ENTER, UPDATE, EXIT
};
public class State
{
    public string name;
    public StateStage stage;
    
    // during compile, state knows its transition conditions, but not the next states
    public Dictionary<string, State> transition; // upon condition met, grab the next state using unique string identifier. Derived classes should populate this
    public Dictionary<StateStage, UnityAction> stageActions; // upon stage change, call the action. Derived classes should populate this
    public State nextState; // next state is set dynamically

    public State()
    {
        transition = new Dictionary<string, State>();
        stageActions = new Dictionary<StateStage, UnityAction>();
        stageActions[StateStage.ENTER] = null;
        stageActions[StateStage.UPDATE] = null;
        stageActions[StateStage.EXIT] = null;
        stage = StateStage.ENTER;
    }

    public virtual void Enter()
    { 
        stage = StateStage.UPDATE; 
        nextState = null; 
        stageActions[StateStage.ENTER]?.Invoke();
    }

    public virtual void Update()
    {
        stage = StateStage.UPDATE;
        stageActions[StateStage.UPDATE]?.Invoke();
    }

    public virtual void Exit()
    { 
        stage = StateStage.ENTER; 
        stageActions[StateStage.EXIT]?.Invoke();
    } // reset stage to enter

    public State Process()
    {
        if (stage == StateStage.ENTER) Enter();
        if (stage == StateStage.UPDATE) Update();
        if (stage == StateStage.EXIT)
        {
            Exit();
            nextState.Enter(); // enter immediately so that fixedUpdate frame will not happen between 'exitted state' and 'not entered state'
            return nextState;
        }
        return this;
    }

    public State ForceTransition(string key)
    {
        if (transition.ContainsKey(key))
        {
            Exit();
            transition[key].Enter();
            return transition[key];
        }
        else
        {
            Debug.LogWarning("State: Transition failed: key not found");
            return this;
        }
    }

    public State ForceTransition(State newState)
    {
        if (newState == this) { return this; }
        Exit();
        newState.Enter();
        return newState;
    }

    public override string ToString()
    {
        return name;
    }
}