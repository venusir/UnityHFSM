using System.Collections.Generic;

namespace UnityHFSM.Graph
{
    public sealed class MachineRuntimeSnapshot
    {
        public string machineId;
        public string machineName;
        public bool isInitialized;
        public bool isActive;
        public string activeStateId;
        public string activeStateName;
        public bool hasPendingTransition;
        public string pendingStateId;
        public string pendingStateName;
        public string pendingTransitionId;
        public bool isExitPending;
        public double stateElapsed;
    }

    public sealed class GraphRuntimeSnapshot
    {
        public double time;
        public bool isStarted;
        public bool isPaused;
        public string graphId;

        public List<string> activePathMachineIds = new List<string>();
        public List<string> activePathStateIds = new List<string>();
        public List<MachineRuntimeSnapshot> machines = new List<MachineRuntimeSnapshot>();
        public List<BlackboardParameterSnapshot> parameters = new List<BlackboardParameterSnapshot>();
        public List<TransitionRecord> transitions = new List<TransitionRecord>();
    }
}
