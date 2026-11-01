using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityHFSM.Graph.Editor
{
    public enum GraphEditorSelectionKind
    {
        None = 0,
        State = 1,
        Transition = 2,
        Pseudo = 3,
        Machine = 4,
        Multi = 5,
    }

    public enum GraphEditorSelectionItemKind
    {
        State = 0,
        Transition = 1,
        Pseudo = 2,
    }

    public readonly struct GraphEditorSelectionItem : IEquatable<GraphEditorSelectionItem>
    {
        public readonly GraphEditorSelectionItemKind Kind;
        public readonly string Id;
        public readonly PseudoNodeKind PseudoKind;

        public GraphEditorSelectionItem(
            GraphEditorSelectionItemKind kind,
            string id,
            PseudoNodeKind pseudoKind = PseudoNodeKind.Entry)
        {
            Kind = kind;
            Id = id ?? string.Empty;
            PseudoKind = pseudoKind;
        }

        public bool Equals(GraphEditorSelectionItem other) =>
            Kind == other.Kind
            && PseudoKind == other.PseudoKind
            && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is GraphEditorSelectionItem other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (int)PseudoKind;
                hash = (hash * 397) ^ (Id != null ? Id.GetHashCode() : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Session-owned selection. The canvas selection is a projection of this
    /// value; views never keep their own selected ids.
    /// </summary>
    public sealed class GraphEditorSelection : IEquatable<GraphEditorSelection>
    {
        private static readonly GraphEditorSelectionItem[] EmptyItems =
            Array.Empty<GraphEditorSelectionItem>();

        public static readonly GraphEditorSelection None =
            new GraphEditorSelection(GraphEditorSelectionKind.None, null, null, null, EmptyItems);

        private readonly GraphEditorSelectionItem[] items;

        private GraphEditorSelection(
            GraphEditorSelectionKind kind,
            string stateId,
            string transitionId,
            string machineId,
            GraphEditorSelectionItem[] items)
        {
            Kind = kind;
            StateId = stateId ?? string.Empty;
            TransitionId = transitionId ?? string.Empty;
            MachineId = machineId ?? string.Empty;
            this.items = items ?? EmptyItems;
        }

        public GraphEditorSelectionKind Kind { get; }
        public string StateId { get; }
        public string TransitionId { get; }
        public string MachineId { get; }
        public IReadOnlyList<GraphEditorSelectionItem> Items => items;
        public bool IsNone => Kind == GraphEditorSelectionKind.None;
        public bool HasSelection => Kind != GraphEditorSelectionKind.None;

        public static GraphEditorSelection State(string stateId) =>
            string.IsNullOrEmpty(stateId)
                ? None
                : new GraphEditorSelection(
                    GraphEditorSelectionKind.State,
                    stateId,
                    null,
                    null,
                    new[] { new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, stateId) });

        public static GraphEditorSelection Transition(string transitionId) =>
            string.IsNullOrEmpty(transitionId)
                ? None
                : new GraphEditorSelection(
                    GraphEditorSelectionKind.Transition,
                    null,
                    transitionId,
                    null,
                    new[]
                    {
                        new GraphEditorSelectionItem(
                            GraphEditorSelectionItemKind.Transition,
                            transitionId),
                    });

        public static GraphEditorSelection Pseudo(PseudoNodeKind kind) =>
            new GraphEditorSelection(
                GraphEditorSelectionKind.Pseudo,
                null,
                null,
                null,
                new[]
                {
                    new GraphEditorSelectionItem(
                        GraphEditorSelectionItemKind.Pseudo,
                        kind.ToString(),
                        kind),
                });

        public static GraphEditorSelection Machine(string machineId) =>
            string.IsNullOrEmpty(machineId)
                ? None
                : new GraphEditorSelection(
                    GraphEditorSelectionKind.Machine,
                    null,
                    null,
                    machineId,
                    EmptyItems);

        public static GraphEditorSelection Multi(IEnumerable<GraphEditorSelectionItem> source)
        {
            if (source == null)
                return None;

            GraphEditorSelectionItem[] distinct = source
                .Distinct()
                .OrderBy(item => (int)item.Kind)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();

            if (distinct.Length == 0)
                return None;

            if (distinct.Length == 1)
            {
                GraphEditorSelectionItem item = distinct[0];
                switch (item.Kind)
                {
                    case GraphEditorSelectionItemKind.State:
                        return State(item.Id);
                    case GraphEditorSelectionItemKind.Transition:
                        return Transition(item.Id);
                    case GraphEditorSelectionItemKind.Pseudo:
                        return Pseudo(item.PseudoKind);
                }
            }

            return new GraphEditorSelection(
                GraphEditorSelectionKind.Multi,
                null,
                null,
                null,
                distinct);
        }

        public bool Contains(GraphEditorSelectionItem item)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Equals(item))
                    return true;
            }

            return false;
        }

        public bool Equals(GraphEditorSelection other)
        {
            if (other == null || Kind != other.Kind)
                return false;

            if (!string.Equals(StateId, other.StateId, StringComparison.Ordinal)
                || !string.Equals(TransitionId, other.TransitionId, StringComparison.Ordinal)
                || !string.Equals(MachineId, other.MachineId, StringComparison.Ordinal)
                || items.Length != other.items.Length)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].Equals(other.items[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is GraphEditorSelection other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (StateId != null ? StateId.GetHashCode() : 0);
                hash = (hash * 397) ^ (TransitionId != null ? TransitionId.GetHashCode() : 0);
                hash = (hash * 397) ^ (MachineId != null ? MachineId.GetHashCode() : 0);

                for (int i = 0; i < items.Length; i++)
                    hash = (hash * 397) ^ items[i].GetHashCode();

                return hash;
            }
        }
    }
}
