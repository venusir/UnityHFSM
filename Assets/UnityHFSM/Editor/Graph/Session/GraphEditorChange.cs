using System;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Coalesced change flags published by the session. StructureRebuild mirrors
    /// GraphEditorController.Changed (full graph rebuild); ModelDirty mirrors
    /// GraphEditorController.Dirty (in-place edit, no rebuild).
    /// </summary>
    [Flags]
    public enum GraphEditorChange
    {
        None = 0,
        Document = 1 << 0,
        StructureRebuild = 1 << 1,
        ModelDirty = 1 << 2,
        Machine = 1 << 3,
        Selection = 1 << 4,
        Mode = 1 << 5,
        Runtime = 1 << 6,
        Targets = 1 << 7,
        Validation = 1 << 8,
        Layout = 1 << 9,
        Console = 1 << 10,
        Status = 1 << 11,
        Interaction = 1 << 12,
        Lock = 1 << 13,
        All = ~0,
    }
}
