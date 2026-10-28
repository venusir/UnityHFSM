using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Runtime state for normal graph states. It dispatches cloned behaviours and
    /// mirrors State's exit-time gate.
    /// </summary>
    public sealed class DataState : StateBase<string>
    {
        private readonly StateRuntime state;

        public DataState(StateRuntime state)
            : base(state.NeedsExitTime, state.IsGhostState)
        {
            this.state = state;
        }

        public override void OnEnter()
        {
            state.Machine.MarkStateEntered(state.Id);

            StateBehaviourContext context = state.Context;
            for (int i = 0; i < state.Behaviours.Count; i++)
            {
                StateBehaviour behaviour = state.Behaviours[i];
                if (behaviour != null)
                    behaviour.OnStateEnter(context);
            }
        }

        public override void OnLogic()
        {
            StateBehaviourContext context = state.Context;
            for (int i = 0; i < state.Behaviours.Count; i++)
            {
                StateBehaviour behaviour = state.Behaviours[i];
                if (behaviour != null)
                    behaviour.OnStateLogic(context);
            }

            if (needsExitTime && fsm.HasPendingTransition && AllCanExit(context))
                fsm.StateCanExit();
        }

        public override void OnExit()
        {
            StateBehaviourContext context = state.Context;
            for (int i = state.Behaviours.Count - 1; i >= 0; i--)
            {
                StateBehaviour behaviour = state.Behaviours[i];
                if (behaviour != null)
                    behaviour.OnStateExit(context);
            }
        }

        public override void OnExitRequest()
        {
            if (AllCanExit(state.Context))
                fsm.StateCanExit();
        }

        private bool AllCanExit(StateBehaviourContext context)
        {
            if (state.Behaviours.Count == 0)
                return true;

            for (int i = 0; i < state.Behaviours.Count; i++)
            {
                StateBehaviour behaviour = state.Behaviours[i];
                if (behaviour != null && !behaviour.CanExit(context))
                    return false;
            }

            return true;
        }
    }
}
