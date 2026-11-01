using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Canvas-only actions routed through commands.</summary>
    public interface IGraphEditorCanvasHost
    {
        void FrameAll();
        void FrameSelection();
        void ResetZoom();
        void SetGridVisible(bool visible);
        Vector2 GetDefaultCreatePosition();
    }
}
