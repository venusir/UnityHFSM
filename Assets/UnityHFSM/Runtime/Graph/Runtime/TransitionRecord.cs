namespace UnityHFSM.Graph
{
    public sealed class TransitionRecord
    {
        public int sequence;
        public double time;
        public string machineId;
        public string machineName;
        public string transitionId;
        public string fromStateId;
        public string fromStateName;
        public string toStateId;
        public string toStateName;
        public bool isExit;
        public float delaySeconds;
    }
}
