using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Editor
{
    public readonly struct GraphEditorNavigationTarget : IEquatable<GraphEditorNavigationTarget>
    {
        public readonly string MachineId;
        public readonly GraphEditorSelection Selection;

        public GraphEditorNavigationTarget(string machineId, GraphEditorSelection selection)
        {
            MachineId = machineId ?? string.Empty;
            Selection = selection ?? GraphEditorSelection.None;
        }

        public bool Equals(GraphEditorNavigationTarget other) =>
            string.Equals(MachineId, other.MachineId, StringComparison.Ordinal)
            && Equals(Selection, other.Selection);

        public override bool Equals(object obj) =>
            obj is GraphEditorNavigationTarget other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((MachineId != null ? MachineId.GetHashCode() : 0) * 397)
                    ^ (Selection != null ? Selection.GetHashCode() : 0);
            }
        }
    }

    /// <summary>
    /// Back/forward navigation over machine + selection snapshots.
    /// </summary>
    public sealed class GraphEditorNavigationHistory
    {
        public const int Capacity = 50;

        private readonly List<GraphEditorNavigationTarget> entries =
            new List<GraphEditorNavigationTarget>();
        private int index = -1;

        public bool CanGoBack => index > 0;
        public bool CanGoForward => index >= 0 && index < entries.Count - 1;
        public int Count => entries.Count;
        public GraphEditorNavigationTarget Current =>
            index >= 0 && index < entries.Count
                ? entries[index]
                : default;

        public void Reset()
        {
            entries.Clear();
            index = -1;
        }

        public void Push(GraphEditorNavigationTarget target)
        {
            if (index >= 0 && entries[index].Equals(target))
                return;

            if (index < entries.Count - 1)
                entries.RemoveRange(index + 1, entries.Count - index - 1);

            entries.Add(target);

            if (entries.Count > Capacity)
            {
                int overflow = entries.Count - Capacity;
                entries.RemoveRange(0, overflow);
            }

            index = entries.Count - 1;
        }

        public bool TryBack(out GraphEditorNavigationTarget target)
        {
            if (!CanGoBack)
            {
                target = default;
                return false;
            }

            index--;
            target = entries[index];
            return true;
        }

        public bool TryForward(out GraphEditorNavigationTarget target)
        {
            if (!CanGoForward)
            {
                target = default;
                return false;
            }

            index++;
            target = entries[index];
            return true;
        }
    }
}
