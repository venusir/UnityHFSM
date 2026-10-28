using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// ScriptableObject container for a state machine graph. The graph is the
    /// authored source of truth; the runtime copies it when building.
    /// </summary>
    public class StateMachineGraphAsset : ScriptableObject
    {
        [SerializeField]
        private GraphModel graph = new GraphModel();

        public GraphModel Graph => graph;

        public void SetGraph(GraphModel model)
        {
            graph = model ?? new GraphModel();
            graph.EnsureLists();
        }

        private void OnEnable()
        {
            if (graph == null)
                graph = new GraphModel();

            graph.EnsureLists();
        }
    }
}
