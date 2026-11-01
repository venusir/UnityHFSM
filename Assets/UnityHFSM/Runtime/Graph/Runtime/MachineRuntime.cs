using System;
using System.Collections.Generic;
using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Runtime copy of one machine definition. It never reads the asset model
    /// during ticks, so editing an asset while playing cannot corrupt a runner.
    /// </summary>
    public sealed class MachineRuntime
    {
        internal GraphRuntime Runtime;
        internal Action<StateBase<string>> StateChangedHandler;

        public string Id { get; }
        public string Name { get; }
        public string ParentStateId { get; }
        public string StartStateId { get; }
        public MachineKind Kind { get; }
        public bool RememberLastState { get; }

        public MachineRuntime Parent { get; internal set; }
        public StateRuntime HostState { get; internal set; }
        public StateMachine<string, string, string> Fsm { get; internal set; }

        public string ActiveStateId { get; internal set; } = string.Empty;
        public string PendingTransitionId { get; internal set; } = string.Empty;
        public double StateEnteredAt { get; internal set; }

        public List<StateRuntime> States { get; } = new List<StateRuntime>();
        public Dictionary<string, StateRuntime> StateById { get; } = new Dictionary<string, StateRuntime>(StringComparer.Ordinal);
        public Dictionary<string, StateRuntime> StateByName { get; } = new Dictionary<string, StateRuntime>(StringComparer.Ordinal);
        public Dictionary<string, string> StateNameToId { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, StateBase<string>> StateObjectsByName { get; } = new Dictionary<string, StateBase<string>>(StringComparer.Ordinal);
        public Dictionary<StateBase<string>, string> StateIdByObject { get; } = new Dictionary<StateBase<string>, string>();

        public double StateElapsed => Runtime != null ? Runtime.Now - StateEnteredAt : 0d;
        public bool IsInitialized => Fsm != null && Fsm.IsInitialized;

        internal MachineRuntime(MachineDef definition)
        {
            Id = definition.id;
            Name = definition.name;
            ParentStateId = definition.parentStateId;
            StartStateId = definition.startStateId;
            Kind = definition.kind;
            RememberLastState = definition.rememberLastState;
        }

        public StateRuntime GetState(string stateId)
        {
            return !string.IsNullOrEmpty(stateId) && StateById.TryGetValue(stateId, out StateRuntime state)
                ? state
                : null;
        }

        public StateRuntime GetStateByName(string stateName)
        {
            return !string.IsNullOrEmpty(stateName) && StateByName.TryGetValue(stateName, out StateRuntime state)
                ? state
                : null;
        }

        public string GetStateName(string stateId)
        {
            StateRuntime state = GetState(stateId);
            return state != null ? state.Name : string.Empty;
        }

        public string GetStateId(string stateName)
        {
            StateRuntime state = GetStateByName(stateName);
            return state != null ? state.Id : string.Empty;
        }

        public void MarkStateEntered(string stateId)
        {
            ActiveStateId = stateId ?? string.Empty;
            PendingTransitionId = string.Empty;
            StateEnteredAt = Runtime != null ? Runtime.Now : 0d;
        }
    }
}
