using UnityEngine;

namespace UnityHFSM.Graph
{
    public sealed class GraphRuntimeOptions
    {
        /// <summary>Optional host used by behaviour contexts.</summary>
        public GameObject Host;

        /// <summary>Factory used to create independent behaviour clones.</summary>
        public IStateBehaviourFactory BehaviourFactory;

        /// <summary>Maximum number of committed transitions retained for debugging.</summary>
        public int TransitionLogCapacity = 256;

        public bool EnableDebugTrace = true;
    }
}
