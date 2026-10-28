using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class TransitionEdge : Edge
    {
        public string TransitionId;
        public bool IsEntryEdge;

        public void SetColor(Color color)
        {
            if (edgeControl == null)
                return;

            edgeControl.inputColor = color;
            edgeControl.outputColor = color;
        }
    }
}
