using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class GraphEdgeConnectorListener : IEdgeConnectorListener
    {
        private readonly StateMachineGraphView view;

        public GraphEdgeConnectorListener(StateMachineGraphView view)
        {
            this.view = view;
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            if (edge != null)
                view.CreateConnection(edge.output, edge.input);
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
        }
    }
}
