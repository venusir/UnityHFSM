using System.Collections.Generic;
using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>Runtime copy of one state definition, including cloned behaviours.</summary>
    public sealed class StateRuntime
    {
        public string Id { get; }
        public string MachineId { get; }
        public string Name { get; }
        public StateKind Kind { get; }
        public string SubMachineId { get; }
        public bool NeedsExitTime { get; }
        public bool IsGhostState { get; }

        public MachineRuntime Machine { get; internal set; }
        public MachineRuntime ChildMachine { get; internal set; }
        public StateBase<string> StateObject { get; internal set; }
        public List<StateBehaviour> Behaviours { get; } = new List<StateBehaviour>();
        public StateBehaviourContext Context { get; internal set; }

        internal StateRuntime(StateDef definition, MachineRuntime machine)
        {
            Id = definition.id;
            MachineId = definition.machineId;
            Name = definition.name;
            Kind = definition.kind;
            SubMachineId = definition.subMachineId;
            NeedsExitTime = definition.needsExitTime;
            IsGhostState = definition.isGhostState;
            Machine = machine;
        }
    }
}
