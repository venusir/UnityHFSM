using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Runtime transition for graph-authored machines. Delay is measured against
    /// the owning machine's current-state elapsed time, which fixes the core
    /// TransitionAfter behavior for Any-State transitions.
    /// </summary>
    public sealed class GraphTransition : TransitionBase<string>
    {
        private readonly GraphRuntime runtime;
        private readonly MachineRuntime machine;
        private readonly ConditionEvaluator evaluator;
        private readonly string transitionId;
        private readonly string fromStateId;
        private readonly string toStateId;
        private readonly float delaySeconds;

        public string TransitionId => transitionId;
        public string MachineId => machine.Id;
        public string MachineName => machine.Name;
        public string FromStateId => fromStateId;
        public string ToStateId => toStateId;
        public float DelaySeconds => delaySeconds;
        public bool IsExit => isExitTransition;

        public GraphTransition(
            GraphRuntime runtime,
            MachineRuntime machine,
            TransitionDef definition,
            string fromName,
            string toName,
            ConditionEvaluator evaluator)
            : base(fromName, toName, definition.forceInstantly)
        {
            this.runtime = runtime;
            this.machine = machine;
            this.evaluator = evaluator;
            transitionId = definition.id;
            fromStateId = definition.fromStateId;
            toStateId = definition.toStateId;
            delaySeconds = definition.delaySeconds;
            isExitTransition = definition.target == TransitionTargetKind.Exit;
        }

        public override void OnEnter()
        {
            // Intentionally empty. Delay is computed from MachineRuntime.StateElapsed.
        }

        public override bool ShouldTransition()
        {
            if (runtime.Now - machine.StateEnteredAt < delaySeconds)
                return false;

            if (!evaluator.EvaluateAndConsume(runtime.Blackboard))
                return false;

            machine.PendingTransitionId = transitionId;
            return true;
        }

        public override void BeforeTransition()
        {
        }

        public override void AfterTransition()
        {
            runtime.RecordTransition(this);
        }
    }
}
