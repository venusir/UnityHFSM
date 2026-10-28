namespace UnityHFSM.Graph
{
    /// <summary>Type of a blackboard parameter, mirroring Animator parameters.</summary>
    public enum BlackboardParameterType
    {
        Float = 0,
        Int = 1,
        Bool = 2,
        Trigger = 3,
    }

    public enum MachineKind
    {
        StateMachine = 0,
        HybridStateMachine = 1,
    }

    public enum StateKind
    {
        State = 0,
        HybridSubMachine = 1,
    }

    public enum TransitionSourceKind
    {
        State = 0,
        Any = 1,
    }

    public enum TransitionTargetKind
    {
        State = 0,
        Exit = 1,
    }

    /// <summary>
    /// Comparison operators for blackboard conditions. Numeric values are part of
    /// the serialized JSON format, so keep them append-only.
    /// </summary>
    public enum ConditionComparison
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5,
        IsTrue = 6,
        IsFalse = 7,
        IsSet = 8,
    }
}
