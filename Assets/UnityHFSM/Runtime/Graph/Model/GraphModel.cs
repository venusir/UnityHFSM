using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Serializable source-of-truth for a graph asset. The model is intentionally
    /// flat (lists + string IDs + enums) so it can be serialized both by Unity and
    /// by JsonUtility without custom converters.
    /// </summary>
    [Serializable]
    public class GraphModel
    {
        public int schemaVersion = 1;
        public string graphId = string.Empty;
        public string rootMachineId = string.Empty;

        public List<MachineDef> machines = new List<MachineDef>();
        public List<StateDef> states = new List<StateDef>();
        public List<TransitionDef> transitions = new List<TransitionDef>();
        public List<ParameterDef> parameters = new List<ParameterDef>();

        public MachineDef FindMachine(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < machines.Count; i++)
            {
                if (machines[i] != null && machines[i].id == id)
                    return machines[i];
            }

            return null;
        }

        public StateDef FindState(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && states[i].id == id)
                    return states[i];
            }

            return null;
        }

        public TransitionDef FindTransition(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] != null && transitions[i].id == id)
                    return transitions[i];
            }

            return null;
        }

        public ParameterDef FindParameter(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && parameters[i].id == id)
                    return parameters[i];
            }

            return null;
        }

        /// <summary>Normalizes null lists after deserialization.</summary>
        public void EnsureLists()
        {
            if (machines == null) machines = new List<MachineDef>();
            if (states == null) states = new List<StateDef>();
            if (transitions == null) transitions = new List<TransitionDef>();
            if (parameters == null) parameters = new List<ParameterDef>();

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && states[i].behaviours == null)
                    states[i].behaviours = new List<StateBehaviour>();
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] != null && transitions[i].conditions == null)
                    transitions[i].conditions = new List<ConditionDef>();
            }
        }
    }

    [Serializable]
    public class MachineDef
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string parentStateId = string.Empty;
        public string startStateId = string.Empty;
        public MachineKind kind = MachineKind.StateMachine;
        public bool rememberLastState;

        // Editor-only pseudo-node positions.
        public Vector2 entryPosition = new Vector2(-360f, -120f);
        public Vector2 anyStatePosition = new Vector2(-360f, 0f);
        public Vector2 exitPosition = new Vector2(360f, 0f);
    }

    [Serializable]
    public class StateDef
    {
        public string id = string.Empty;
        public string machineId = string.Empty;
        public string name = string.Empty;
        public StateKind kind = StateKind.State;
        public string subMachineId = string.Empty;
        public bool needsExitTime;
        public bool isGhostState;
        public List<StateBehaviour> behaviours = new List<StateBehaviour>();
        public Vector2 position;
    }

    [Serializable]
    public class TransitionDef
    {
        public string id = string.Empty;
        public string machineId = string.Empty;
        public TransitionSourceKind source = TransitionSourceKind.State;
        public string fromStateId = string.Empty;
        public TransitionTargetKind target = TransitionTargetKind.State;
        public string toStateId = string.Empty;
        public float delaySeconds;
        public bool forceInstantly;
        public int order;
        public List<ConditionDef> conditions = new List<ConditionDef>();
    }

    [Serializable]
    public class ParameterDef
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public BlackboardParameterType type = BlackboardParameterType.Float;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
        public bool exposed = true;
    }

    [Serializable]
    public class ConditionDef
    {
        public string parameterId = string.Empty;
        public ConditionComparison comparison = ConditionComparison.Equal;
        public float floatValue;
        public int intValue;
    }
}
