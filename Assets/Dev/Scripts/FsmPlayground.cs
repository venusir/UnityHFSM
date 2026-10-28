using UnityEngine;
using UnityHFSM;

/// <summary>
/// Small smoke-test state machine used by the development project.
/// </summary>
public class FsmPlayground : MonoBehaviour
{
    private StateMachine fsm;

    private void Start()
    {
        fsm = new StateMachine();
        fsm.AddState("Idle", onEnter: _ => Debug.Log("Enter Idle"));
        fsm.AddState("Move", onEnter: _ => Debug.Log("Enter Move"));
        fsm.AddTransition("Idle", "Move", t => Time.time > 1f);
        fsm.AddTransition("Move", "Idle", t => Time.time > 2f);
        fsm.SetStartState("Idle");
        fsm.Init();
    }

    private void Update()
    {
        fsm.OnLogic();
    }
}
