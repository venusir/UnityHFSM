using System;

namespace UnityHFSM.Graph.Editor
{
    [Serializable]
    public sealed class GraphEditorLayoutState
    {
        public float parametersWidth = 250f;
        public float inspectorWidth = 320f;
        public float consoleHeight = 220f;
        public bool parametersVisible = true;
        public bool inspectorVisible = true;
        public float previewSpeed = 1f;
    }
}
