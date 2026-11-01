using System;

namespace UnityHFSM.Graph.Editor
{
    public enum GraphConsoleTab
    {
        Validation = 0,
        RuntimeLog = 1,
    }

    [Serializable]
    public sealed class GraphEditorConsoleState
    {
        public bool visible;
        public GraphConsoleTab tab = GraphConsoleTab.Validation;
    }
}
