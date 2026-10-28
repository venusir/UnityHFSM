using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Flat JSON DTO. It mirrors GraphModel but stores behaviour references as
    /// asset GUID strings so JsonUtility can round-trip the document.
    /// </summary>
    [Serializable]
    public class GraphDocumentDto
    {
        public string format = "unityhfsm.graph";
        public int schemaVersion = 1;
        public string graphId = string.Empty;
        public string rootMachineId = string.Empty;
        public List<DtoMachine> machines = new List<DtoMachine>();
        public List<DtoState> states = new List<DtoState>();
        public List<DtoTransition> transitions = new List<DtoTransition>();
        public List<DtoParameter> parameters = new List<DtoParameter>();
    }

    [Serializable]
    public class DtoMachine
    {
        public string id;
        public string name;
        public string parentStateId;
        public string startStateId;
        public MachineKind kind;
        public bool rememberLastState;
        public Vector2 entryPosition;
        public Vector2 anyStatePosition;
        public Vector2 exitPosition;
    }

    [Serializable]
    public class DtoState
    {
        public string id;
        public string machineId;
        public string name;
        public StateKind kind;
        public string subMachineId;
        public bool needsExitTime;
        public bool isGhostState;
        public List<string> behaviourGuids = new List<string>();
        public Vector2 position;
    }

    [Serializable]
    public class DtoTransition
    {
        public string id;
        public string machineId;
        public TransitionSourceKind source;
        public string fromStateId;
        public TransitionTargetKind target;
        public string toStateId;
        public float delaySeconds;
        public bool forceInstantly;
        public int order;
        public List<DtoCondition> conditions = new List<DtoCondition>();
    }

    [Serializable]
    public class DtoCondition
    {
        public string parameterId;
        public ConditionComparison comparison;
        public float floatValue;
        public int intValue;
    }

    [Serializable]
    public class DtoParameter
    {
        public string id;
        public string name;
        public BlackboardParameterType type;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
        public bool exposed;
    }
}
