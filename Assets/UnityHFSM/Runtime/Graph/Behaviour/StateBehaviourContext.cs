using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Runtime context passed to state behaviours. It deliberately exposes IDs and
    /// names rather than editor model objects.
    /// </summary>
    public sealed class StateBehaviourContext
    {
        public GraphRuntime Runtime { get; }
        public Blackboard Blackboard => Runtime != null ? Runtime.Blackboard : null;
        public string MachineId { get; }
        public string MachineName { get; }
        public string StateId { get; }
        public string StateName { get; }
        public GameObject Host { get; }

        public float StateElapsed => Runtime != null ? (float)Runtime.GetStateElapsed(MachineId) : 0f;
        public double Time => Runtime != null ? Runtime.Now : 0d;

        internal StateBehaviourContext(
            GraphRuntime runtime,
            MachineRuntime machine,
            StateRuntime state,
            GameObject host)
        {
            Runtime = runtime;
            MachineId = machine != null ? machine.Id : string.Empty;
            MachineName = machine != null ? machine.Name : string.Empty;
            StateId = state != null ? state.Id : string.Empty;
            StateName = state != null ? state.Name : string.Empty;
            Host = host;
        }

        public void RequestStateChange(string stateId, bool forceInstantly = false)
        {
            Runtime?.RequestStateChange(MachineId, stateId, forceInstantly);
        }

        public void RequestExit(bool forceInstantly = false)
        {
            Runtime?.RequestExit(MachineId, forceInstantly);
        }
    }
}
