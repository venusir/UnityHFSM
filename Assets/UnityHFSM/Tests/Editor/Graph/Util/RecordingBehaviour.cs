using System.Collections.Generic;
using UnityHFSM.Graph;

namespace UnityHFSM.Graph.Tests
{
    public sealed class RecordingBehaviour : StateBehaviour
    {
        public static readonly List<string> Events = new List<string>();
        public static bool AllowExit = true;

        public override void OnStateEnter(StateBehaviourContext context)
        {
            Events.Add("enter:" + context.StateName);
        }

        public override void OnStateLogic(StateBehaviourContext context)
        {
            Events.Add("logic:" + context.StateName);
        }

        public override void OnStateExit(StateBehaviourContext context)
        {
            Events.Add("exit:" + context.StateName);
        }

        public override bool CanExit(StateBehaviourContext context)
        {
            return AllowExit;
        }

        public override void OnStateMachineEnter(StateBehaviourContext context)
        {
            Events.Add("machine-enter:" + context.StateName);
        }

        public override void OnStateMachineExit(StateBehaviourContext context)
        {
            Events.Add("machine-exit:" + context.StateName);
        }
    }
}
