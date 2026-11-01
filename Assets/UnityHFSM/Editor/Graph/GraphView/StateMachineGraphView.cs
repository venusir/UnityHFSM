using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// GraphView for one machine at a time. The model is the only source of truth;
    /// view elements only carry stable IDs.
    /// </summary>
    public sealed class StateMachineGraphView : GraphView
    {
        private static readonly Vector2 StateNodeSize = new Vector2(180f, 120f);
        private static readonly Vector2 PseudoNodeSize = new Vector2(130f, 60f);
        private static readonly Color PendingEdgeColor = new Color(1f, 0.75f, 0.20f);
        private static readonly Color FlashEdgeColor = new Color(0.30f, 0.90f, 0.45f);

        private readonly Dictionary<string, StateGraphNode> stateNodesById =
            new Dictionary<string, StateGraphNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, TransitionEdge> edgesByTransitionId =
            new Dictionary<string, TransitionEdge>(StringComparer.Ordinal);

        private PseudoGraphNode entryNode;
        private PseudoGraphNode anyStateNode;
        private PseudoGraphNode exitNode;
        private GridBackground grid;
        private bool gridVisible = true;
        private bool suppressChanges;
        private int lastFlashSequence = -1;
        private string flashTransitionId;
        private double flashUntil;

        public GraphEditorController Controller { get; set; }
        public string CurrentMachineId { get; private set; }
        public GraphEdgeConnectorListener EdgeConnectorListener { get; }
        public Action<string> OpenSubMachineRequested { get; set; }

        public StateMachineGraphView()
        {
            EdgeConnectorListener = new GraphEdgeConnectorListener(this);
            style.flexGrow = 1f;

            grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            graphViewChanged = OnGraphViewChanged;
        }

        public void LoadMachine(string machineId)
        {
            CurrentMachineId = machineId;
            ClearGraph();
            lastFlashSequence = -1;
            flashTransitionId = null;
            flashUntil = 0d;

            if (Controller == null || Controller.Asset == null || Controller.Model == null)
                return;

            MachineDef machine = Controller.FindMachine(machineId);
            if (machine == null)
                return;

            suppressChanges = true;
            graphViewChanged = null;

            bool isRoot = string.IsNullOrEmpty(machine.parentStateId);

            entryNode = new PseudoGraphNode(PseudoNodeKind.Entry, machineId, this, true);
            anyStateNode = new PseudoGraphNode(PseudoNodeKind.AnyState, machineId, this, true);
            exitNode = new PseudoGraphNode(PseudoNodeKind.Exit, machineId, this, !isRoot);

            entryNode.SetPosition(new Rect(machine.entryPosition, PseudoNodeSize));
            anyStateNode.SetPosition(new Rect(machine.anyStatePosition, PseudoNodeSize));
            exitNode.SetPosition(new Rect(machine.exitPosition, PseudoNodeSize));

            AddElement(entryNode);
            AddElement(anyStateNode);
            AddElement(exitNode);

            GraphModel model = Controller.Model;
            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state == null || state.machineId != machineId)
                    continue;

                var node = new StateGraphNode(
                    state.id,
                    machineId,
                    state.name,
                    state.kind == StateKind.HybridSubMachine,
                    OpenSubMachine,
                    this);

                node.SetPosition(new Rect(state.position, StateNodeSize));
                node.SetBadges(BuildStateBadges(state));
                AddElement(node);
                stateNodesById[state.id] = node;
            }

            if (stateNodesById.TryGetValue(machine.startStateId, out StateGraphNode startNode))
            {
                AddTransitionEdge(
                    entryNode.OutputPort,
                    startNode.InputPort,
                    string.Empty,
                    true,
                    new Color(0.25f, 0.75f, 0.35f),
                    "The machine's start state.");
            }

            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null || transition.machineId != machineId)
                    continue;

                Port outputPort = null;
                Port inputPort = null;

                if (transition.source == TransitionSourceKind.Any)
                {
                    outputPort = anyStateNode.OutputPort;
                }
                else if (transition.fromStateId != null
                    && stateNodesById.TryGetValue(transition.fromStateId, out StateGraphNode fromNode))
                {
                    outputPort = fromNode.OutputPort;
                }

                if (transition.target == TransitionTargetKind.Exit)
                {
                    inputPort = exitNode.InputPort;
                }
                else if (transition.toStateId != null
                    && stateNodesById.TryGetValue(transition.toStateId, out StateGraphNode toNode))
                {
                    inputPort = toNode.InputPort;
                }

                if (outputPort == null || inputPort == null)
                    continue;

                Color color = transition.target == TransitionTargetKind.Exit
                    ? new Color(0.85f, 0.35f, 0.35f)
                    : transition.source == TransitionSourceKind.Any
                        ? new Color(0.85f, 0.60f, 0.25f)
                        : new Color(0.55f, 0.55f, 0.55f);

                AddTransitionEdge(
                    outputPort,
                    inputPort,
                    transition.id,
                    false,
                    color,
                    BuildTransitionTooltip(transition));
            }

            graphViewChanged = OnGraphViewChanged;
            suppressChanges = false;
        }

        public void RefreshGraph()
        {
            if (!string.IsNullOrEmpty(CurrentMachineId))
                LoadMachine(CurrentMachineId);
        }

        public void FrameGraph()
        {
            FrameAll();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is GraphView)
            {
                evt.menu.AppendAction("Frame All", _ => FrameGraph());
                evt.menu.AppendSeparator();

                bool canCreate = Controller != null && !string.IsNullOrEmpty(CurrentMachineId);
                Vector2 position = canCreate
                    ? contentViewContainer.WorldToLocal(evt.mousePosition) - StateNodeSize * 0.5f
                    : Vector2.zero;

                evt.menu.AppendAction(
                    "Create State",
                    _ => CreateStateAt(position),
                    canCreate ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

                evt.menu.AppendAction(
                    "Create Sub-State Machine",
                    _ => CreateSubMachineAt(position),
                    canCreate ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

                evt.menu.AppendSeparator();
            }

            evt.menu.AppendAction(
                "Delete",
                _ => DeleteSelection(),
                HasDeletableSelection()
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        public string CreateStateAt(Vector2 position)
        {
            if (Controller == null || string.IsNullOrEmpty(CurrentMachineId))
                return null;

            string stateId = Controller.AddState(CurrentMachineId, position);
            SelectCreatedNode(stateId);
            return stateId;
        }

        public string CreateSubMachineAt(Vector2 position)
        {
            if (Controller == null || string.IsNullOrEmpty(CurrentMachineId))
                return null;

            string hostStateId = Controller.AddSubMachine(CurrentMachineId, position);
            SelectCreatedNode(hostStateId);
            return hostStateId;
        }

        private void SelectCreatedNode(string stateId)
        {
            if (string.IsNullOrEmpty(stateId))
                return;

            if (!stateNodesById.TryGetValue(stateId, out StateGraphNode node))
            {
                RefreshGraph();
                stateNodesById.TryGetValue(stateId, out node);
            }

            if (node == null)
                return;

            ClearSelection();
            AddToSelection(node);
        }

        private bool HasDeletableSelection()
        {
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i] is StateGraphNode)
                    return true;

                if (selection[i] is TransitionEdge edge
                    && !edge.IsEntryEdge
                    && !string.IsNullOrEmpty(edge.TransitionId))
                {
                    return true;
                }
            }

            return false;
        }

        public void CreateConnection(Port outputPort, Port inputPort)
        {
            if (Controller == null || string.IsNullOrEmpty(CurrentMachineId) || outputPort == null || inputPort == null)
                return;

            Node fromNode = outputPort.node;
            Node toNode = inputPort.node;
            if (fromNode == null || toNode == null)
                return;

            if (fromNode is PseudoGraphNode entry && entry.Kind == PseudoNodeKind.Entry)
            {
                if (toNode is StateGraphNode startTarget)
                {
                    Controller.SetStartState(CurrentMachineId, startTarget.StateId);
                    RefreshGraph();
                }

                return;
            }

            TransitionSourceKind source;
            string fromStateId;

            if (fromNode is StateGraphNode fromState)
            {
                source = TransitionSourceKind.State;
                fromStateId = fromState.StateId;
            }
            else if (fromNode is PseudoGraphNode any && any.Kind == PseudoNodeKind.AnyState)
            {
                source = TransitionSourceKind.Any;
                fromStateId = string.Empty;
            }
            else
            {
                return;
            }

            TransitionTargetKind target;
            string toStateId;

            if (toNode is StateGraphNode toState)
            {
                target = TransitionTargetKind.State;
                toStateId = toState.StateId;
            }
            else if (toNode is PseudoGraphNode exit && exit.Kind == PseudoNodeKind.Exit && exit.ExitEnabled)
            {
                target = TransitionTargetKind.Exit;
                toStateId = string.Empty;
            }
            else
            {
                return;
            }

            Controller.AddTransition(CurrentMachineId, source, fromStateId, target, toStateId);
            RefreshGraph();
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            if (startPort == null)
                return compatible;

            ports.ForEach(port =>
            {
                if (port == null || port == startPort || port.direction == startPort.direction)
                    return;

                Node startNode = startPort.node;
                Node endNode = port.node;
                if (startNode == null || endNode == null)
                    return;

                if (startPort.direction == Direction.Output)
                {
                    if (IsOutputNode(startNode) && IsInputNode(endNode))
                        compatible.Add(port);
                }
                else if (IsInputNode(startNode) && IsOutputNode(endNode))
                {
                    compatible.Add(port);
                }
            });

            return compatible;
        }

        public void ApplySnapshot(GraphRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                ClearRuntimeHighlight();
                return;
            }

            var active = new HashSet<string>(snapshot.activePathStateIds);
            string pendingStateId = null;
            string pendingTransitionId = null;
            bool exitPending = false;

            for (int i = 0; i < snapshot.machines.Count; i++)
            {
                MachineRuntimeSnapshot machine = snapshot.machines[i];
                if (machine.machineId == CurrentMachineId)
                {
                    pendingStateId = machine.pendingStateId;
                    pendingTransitionId = machine.pendingTransitionId;
                    exitPending = machine.isExitPending;
                    break;
                }
            }

            foreach (KeyValuePair<string, StateGraphNode> pair in stateNodesById)
            {
                SetHighlight(
                    pair.Value,
                    active.Contains(pair.Key),
                    pair.Key == pendingStateId);
            }

            if (entryNode != null) SetHighlight(entryNode, false, false);
            if (anyStateNode != null) SetHighlight(anyStateNode, false, false);
            if (exitNode != null) SetHighlight(exitNode, false, exitPending);

            UpdateEdgeHighlights(snapshot, pendingStateId, pendingTransitionId);
        }

        public void ClearRuntimeHighlight()
        {
            foreach (StateGraphNode node in stateNodesById.Values)
                SetHighlight(node, false, false);

            if (entryNode != null) SetHighlight(entryNode, false, false);
            if (anyStateNode != null) SetHighlight(anyStateNode, false, false);
            if (exitNode != null) SetHighlight(exitNode, false, false);

            foreach (TransitionEdge edge in edgesByTransitionId.Values)
                edge.ClearHighlight();

            flashTransitionId = null;
            flashUntil = 0d;
        }

        private void UpdateEdgeHighlights(
            GraphRuntimeSnapshot snapshot,
            string pendingStateId,
            string pendingTransitionId)
        {
            TransitionRecord latest = null;

            for (int i = snapshot.transitions.Count - 1; i >= 0; i--)
            {
                TransitionRecord record = snapshot.transitions[i];
                if (record != null && record.machineId == CurrentMachineId)
                {
                    latest = record;
                    break;
                }
            }

            double now = EditorApplication.timeSinceStartup;

            if (latest != null && latest.sequence != lastFlashSequence)
            {
                lastFlashSequence = latest.sequence;
                flashTransitionId = latest.transitionId;
                flashUntil = now + 0.4d;
            }

            if (!string.IsNullOrEmpty(flashTransitionId) && now > flashUntil)
                flashTransitionId = null;

            foreach (KeyValuePair<string, TransitionEdge> pair in edgesByTransitionId)
            {
                string transitionId = pair.Key;
                TransitionEdge edge = pair.Value;

                if (!string.IsNullOrEmpty(flashTransitionId) && flashTransitionId == transitionId)
                {
                    edge.SetHighlight(FlashEdgeColor, 3f);
                    continue;
                }

                if (!string.IsNullOrEmpty(pendingTransitionId) && pendingTransitionId == transitionId)
                {
                    edge.SetHighlight(PendingEdgeColor, 2.5f);
                    continue;
                }

                // Older snapshots without a pending transition id fall back to
                // highlighting every edge that enters the pending state.
                if (string.IsNullOrEmpty(pendingTransitionId)
                    && !string.IsNullOrEmpty(pendingStateId)
                    && EdgeEntersState(edge, pendingStateId))
                {
                    edge.SetHighlight(PendingEdgeColor, 2.5f);
                    continue;
                }

                edge.ClearHighlight();
            }
        }

        private static bool EdgeEntersState(TransitionEdge edge, string stateId)
        {
            return edge.input != null
                && edge.input.node is StateGraphNode target
                && target.StateId == stateId;
        }

        public bool IsGridVisible => gridVisible;

        public void SetGridVisible(bool visible)
        {
            gridVisible = visible;

            if (grid != null)
                grid.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RefreshDecorations()
        {
            if (Controller == null || Controller.Model == null)
                return;

            foreach (KeyValuePair<string, StateGraphNode> pair in stateNodesById)
            {
                StateDef state = Controller.FindState(pair.Key);
                pair.Value.SetBadges(BuildStateBadges(state));
            }

            for (int i = 0; i < Controller.Model.transitions.Count; i++)
            {
                TransitionDef transition = Controller.Model.transitions[i];
                if (transition == null || string.IsNullOrEmpty(transition.id))
                    continue;

                if (edgesByTransitionId.TryGetValue(transition.id, out TransitionEdge edge))
                    edge.tooltip = BuildTransitionTooltip(transition);
            }
        }

        public void SelectState(string stateId)
        {
            if (string.IsNullOrEmpty(stateId)
                || !stateNodesById.TryGetValue(stateId, out StateGraphNode node))
            {
                return;
            }

            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        public void SelectTransition(string transitionId)
        {
            if (string.IsNullOrEmpty(transitionId)
                || !edgesByTransitionId.TryGetValue(transitionId, out TransitionEdge edge))
            {
                return;
            }

            ClearSelection();
            AddToSelection(edge);
            FrameSelection();
        }

        public void FrameSelectionSafe()
        {
            if (selection == null || selection.Count == 0)
                FrameAll();
            else
                FrameSelection();
        }

        public void ResetZoom()
        {
            UpdateViewTransform(Vector3.zero, Vector3.one);
        }

        private static string BuildStateBadges(StateDef state)
        {
            if (state == null)
                return string.Empty;

            var parts = new List<string>();

            if (state.kind == StateKind.HybridSubMachine)
                parts.Add("Sub-Machine");

            if (state.needsExitTime)
                parts.Add("Exit Time");

            if (state.isGhostState)
                parts.Add("Ghost");

            int behaviourCount = 0;
            if (state.behaviours != null)
            {
                for (int i = 0; i < state.behaviours.Count; i++)
                {
                    if (state.behaviours[i] != null)
                        behaviourCount++;
                }
            }

            if (behaviourCount > 0)
                parts.Add(behaviourCount == 1 ? "1 behaviour" : behaviourCount + " behaviours");

            return string.Join(" · ", parts);
        }

        private string BuildTransitionTooltip(TransitionDef transition)
        {
            if (transition == null)
                return string.Empty;

            string fromName = transition.source == TransitionSourceKind.Any
                ? "Any State"
                : StateName(transition.fromStateId);
            string toName = transition.target == TransitionTargetKind.Exit
                ? "Exit"
                : StateName(transition.toStateId);

            var builder = new System.Text.StringBuilder();
            builder.Append(fromName).Append(" -> ").Append(toName);
            builder.Append("\nDelay: ").Append(transition.delaySeconds.ToString("0.###")).Append(" s");
            builder.Append("\nOrder: ").Append(transition.order);

            if (transition.forceInstantly)
                builder.Append("\nForce instantly");

            if (transition.conditions != null && transition.conditions.Count > 0)
            {
                builder.Append("\nConditions:");
                for (int i = 0; i < transition.conditions.Count; i++)
                {
                    ConditionDef condition = transition.conditions[i];
                    if (condition == null)
                        continue;

                    ParameterDef parameter = Controller != null ? Controller.FindParameter(condition.parameterId) : null;
                    string name = parameter != null ? parameter.name : "(missing parameter)";
                    builder.Append("\n  ").Append(name).Append(' ')
                        .Append(condition.comparison);

                    if (parameter != null)
                    {
                        switch (parameter.type)
                        {
                            case BlackboardParameterType.Float:
                                builder.Append(' ').Append(condition.floatValue.ToString("0.###"));
                                break;
                            case BlackboardParameterType.Int:
                                builder.Append(' ').Append(condition.intValue);
                                break;
                        }
                    }
                }
            }

            return builder.ToString();
        }

        private string StateName(string stateId)
        {
            StateDef state = Controller != null ? Controller.FindState(stateId) : null;
            return state != null ? state.name : "(missing)";
        }

        private void ClearGraph()
        {
            graphViewChanged = null;

            foreach (Edge edge in edges.ToList())
                RemoveElement(edge);

            foreach (Node node in nodes.ToList())
                RemoveElement(node);

            stateNodesById.Clear();
            edgesByTransitionId.Clear();
            entryNode = null;
            anyStateNode = null;
            exitNode = null;

            graphViewChanged = OnGraphViewChanged;
        }

        private void AddTransitionEdge(
            Port outputPort,
            Port inputPort,
            string transitionId,
            bool isEntry,
            Color color,
            string tooltip)
        {
            if (outputPort == null || inputPort == null)
                return;

            var edge = new TransitionEdge
            {
                output = outputPort,
                input = inputPort,
                TransitionId = transitionId,
                IsEntryEdge = isEntry,
                tooltip = tooltip,
            };

            if (isEntry)
                edge.capabilities &= ~Capabilities.Deletable;

            AddElement(edge);
            edge.SetBaseColor(color);

            if (!string.IsNullOrEmpty(transitionId))
                edgesByTransitionId[transitionId] = edge;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (suppressChanges || Controller == null)
                return change;

            if (change.movedElements != null)
            {
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    GraphElement element = change.movedElements[i];

                    if (element is StateGraphNode stateNode)
                    {
                        Controller.MoveState(stateNode.StateId, stateNode.GetPosition().position);
                    }
                    else if (element is PseudoGraphNode pseudoNode)
                    {
                        Controller.MovePseudoNode(
                            CurrentMachineId,
                            pseudoNode.Kind,
                            pseudoNode.GetPosition().position);
                    }
                }
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                var stateIds = new List<string>();
                var transitionIds = new List<string>();

                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    GraphElement element = change.elementsToRemove[i];

                    if (element is StateGraphNode stateNode)
                        stateIds.Add(stateNode.StateId);
                    else if (element is TransitionEdge edge && !string.IsNullOrEmpty(edge.TransitionId))
                        transitionIds.Add(edge.TransitionId);
                }

                // Remove through the controller and rebuild from the model.
                change.elementsToRemove = null;

                if (stateIds.Count > 0 || transitionIds.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (Controller == null)
                            return;

                        for (int i = 0; i < stateIds.Count; i++)
                            Controller.DeleteState(stateIds[i]);

                        for (int i = 0; i < transitionIds.Count; i++)
                            Controller.DeleteTransition(transitionIds[i]);

                        RefreshGraph();
                    };
                }
            }

            return change;
        }

        private void OpenSubMachine(string hostStateId)
        {
            if (Controller == null)
                return;

            StateDef host = Controller.FindState(hostStateId);
            if (host != null && !string.IsNullOrEmpty(host.subMachineId))
                OpenSubMachineRequested?.Invoke(host.subMachineId);
        }

        private static bool IsOutputNode(Node node)
        {
            if (node is StateGraphNode)
                return true;

            if (node is PseudoGraphNode pseudo)
                return pseudo.Kind == PseudoNodeKind.Entry || pseudo.Kind == PseudoNodeKind.AnyState;

            return false;
        }

        private static bool IsInputNode(Node node)
        {
            if (node is StateGraphNode)
                return true;

            if (node is PseudoGraphNode pseudo)
                return pseudo.Kind == PseudoNodeKind.Exit && pseudo.ExitEnabled;

            return false;
        }

        private static void SetHighlight(Node node, bool active, bool pending)
        {
            Color color = active
                ? new Color(0.25f, 0.85f, 0.30f)
                : pending
                    ? new Color(1f, 0.75f, 0.20f)
                    : Color.clear;

            float width = color.a > 0f ? 3f : 0f;
            var styleColor = new StyleColor(color);

            node.style.borderLeftWidth = width;
            node.style.borderRightWidth = width;
            node.style.borderTopWidth = width;
            node.style.borderBottomWidth = width;
            node.style.borderLeftColor = styleColor;
            node.style.borderRightColor = styleColor;
            node.style.borderTopColor = styleColor;
            node.style.borderBottomColor = styleColor;
        }
    }
}
