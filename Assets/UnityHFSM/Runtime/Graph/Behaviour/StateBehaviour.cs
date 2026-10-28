using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Animator-like state behaviour. Instances are cloned per runner, so fields
    /// on the clone can hold per-instance state without mutating the asset.
    /// </summary>
    public abstract class StateBehaviour : ScriptableObject
    {
        public virtual void OnStateEnter(StateBehaviourContext context) { }
        public virtual void OnStateLogic(StateBehaviourContext context) { }
        public virtual void OnStateExit(StateBehaviourContext context) { }
        public virtual bool CanExit(StateBehaviourContext context) => true;
        public virtual void OnStateMachineEnter(StateBehaviourContext context) { }
        public virtual void OnStateMachineExit(StateBehaviourContext context) { }
    }
}
