using System;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Serialized window state. The window owns exactly one of these; the
    /// session receives the same instance and writes changes back.
    /// </summary>
    [Serializable]
    public sealed class GraphEditorWindowState
    {
        public StateMachineGraphAsset asset;
        public StateMachineRunner followedRunner;
        public bool selectionLocked;
        public GraphEditorLayoutState layout = new GraphEditorLayoutState();
        public GraphEditorConsoleState console = new GraphEditorConsoleState();
        public bool gridVisible = true;
        public string lastMachineId = string.Empty;
        public string selectedStateId = string.Empty;
        public string selectedTransitionId = string.Empty;
        public string previewHostGlobalId = string.Empty;
    }
}
