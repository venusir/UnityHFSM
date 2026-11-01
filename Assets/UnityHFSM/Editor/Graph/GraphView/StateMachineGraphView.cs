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
    public sealed class StateMachineGraphView : GraphView, IGraphEditorCanvasHost
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
        private bool readOnly;
        private GraphEditorSession session;
        private GraphEditorCommands commands;
        private bool suppressSelectionSync;
        private Label emptyHint;
        private StateGraphNode linkSourceNode;
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

            emptyHint = new Label();
            emptyHint.style.position = Position.Absolute;
            emptyHint.style.left = 12f;
            emptyHint.style.top = 12f;
            emptyHint.style.fontSize = GraphEditorMetrics.FontSize;
            emptyHint.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            emptyHint.style.display = DisplayStyle.None;
            emptyHint.pickingMode = PickingMode.Ignore;
            hierarchy.Add(emptyHint);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            graphViewChanged = OnGraphViewChanged;
        }

        public void Bind(GraphEditorSession session, GraphEditorCommands commands)
        {
            Unbind();
            this.session = session;
            this.commands = commands;

            if (session == null)
            {
                Controller = null;
                return;
            }

            Controller = session.Controller;
            session.Changed += OnSessionChanged;
            OnSessionChanged(GraphEditorChange.All);
        }

        public void Unbind()
        {
            if (session != null)
                session.Changed -= OnSessionChanged;

            session = null;
            commands = null;
            Controller = null;
            ClearGraph();
        }

        private void OnSessionChanged(GraphEditorChange change)
        {
            if (session == null)
                return;

            bool documentChanged = (change & GraphEditorChange.Document) != 0;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.StructureRebuild
                | GraphEditorChange.Machine
                | GraphEditorChange.All)) != 0)
            {
                Controller = session.Controller;
                LoadMachine(session.CurrentMachineId);
                ApplySelection(session.Selection);
                SetReadOnly(GraphEditorModePolicy.IsReadOnlyGraph(session.Mode));
                SetGridVisible(session.GridVisible);
                UpdateLinkHighlight();

                if (documentChanged)
                    FrameAll();
            }

            if ((change & GraphEditorChange.Mode) != 0)
                SetReadOnly(GraphEditorModePolicy.IsReadOnlyGraph(session.Mode));

            if ((change & GraphEditorChange.Selection) != 0)
                ApplySelection(session.Selection);

            if ((change & GraphEditorChange.Runtime) != 0)
                ApplySnapshot(session.Snapshot);

            if ((change & GraphEditorChange.Layout) != 0)
                SetGridVisible(session.GridVisible);

            if ((change & GraphEditorChange.ModelDirty) != 0)
                RefreshDecorations();

            if ((change & GraphEditorChange.Interaction) != 0)
                UpdateLinkHighlight();
        }

        private void UpdateLinkHighlight()
        {
            if (linkSourceNode != null)
            {
                SetHighlight(linkSourceNode, false, false);
                linkSourceNode = null;
            }

            if (session == null || string.IsNullOrEmpty(session.PendingLinkSourceId))
                return;

            if (stateNodesById.TryGetValue(session.PendingLinkSourceId, out StateGraphNode node))
            {
                linkSourceNode = node;
                SetHighlight(node, false, true);
            }
        }

        /// <summary>Call from the window after session.Tick to project GraphView selection into the session.</summary>
        public void PollSelection()
        {
            if (session == null || commands == null || suppressSelectionSync)
                return;

            if (session.IsLinking)
            {
                if (selection != null && selection.Count == 1 && selection[0] is StateGraphNode linkTarget)
                {
                    if (string.IsNullOrEmpty(session.PendingLinkSourceId))
                    {
                        commands.Authoring.SetLinkSource(linkTarget.StateId);
                        ClearSelection();
                    }
                    else
                    {
                        commands.Authoring.CompleteLink(linkTarget.StateId);
                    }
                }

                return;
            }

            GraphEditorSelection next = BuildSelectionFromCanvas();
            if (!next.Equals(session.Selection))
                commands.Navigation.Select(next, true);
        }

        private GraphEditorSelection BuildSelectionFromCanvas()
        {
            var items = new List<GraphEditorSelectionItem>();

            if (selection != null)
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    ISelectable element = selection[i];
                    if (element is StateGraphNode state)
                        items.Add(new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, state.StateId));
                    else if (element is TransitionEdge edge && !edge.IsEntryEdge
                        && !string.IsNullOrEmpty(edge.TransitionId))
                        items.Add(new GraphEditorSelectionItem(
                            GraphEditorSelectionItemKind.Transition,
                            edge.TransitionId));
                    else if (element is PseudoGraphNode pseudo
                        && (pseudo.Kind == PseudoNodeKind.Entry || pseudo.Kind == PseudoNodeKind.AnyState))
                        items.Add(new GraphEditorSelectionItem(
                            GraphEditorSelectionItemKind.Pseudo,
                            pseudo.Kind.ToString(),
                            pseudo.Kind));
                }
            }

            if (items.Count == 0)
                return GraphEditorSelection.Machine(session.CurrentMachineId);

            return GraphEditorSelection.Multi(items);
        }

        private void ApplySelection(GraphEditorSelection desired)
        {
            suppressSelectionSync = true;
            try
            {
                ClearSelection();
                if (desired == null || desired.Kind == GraphEditorSelectionKind.Machine
                    || desired.Kind == GraphEditorSelectionKind.None)
                {
                    return;
                }

                for (int i = 0; i < desired.Items.Count; i++)
                {
                    GraphEditorSelectionItem item = desired.Items[i];
                    switch (item.Kind)
                    {
                        case GraphEditorSelectionItemKind.State:
                            if (stateNodesById.TryGetValue(item.Id, out StateGraphNode state))
                                AddToSelection(state);
                            break;
                        case GraphEditorSelectionItemKind.Transition:
                            if (edgesByTransitionId.TryGetValue(item.Id, out TransitionEdge edge))
                                AddToSelection(edge);
                            break;
                        case GraphEditorSelectionItemKind.Pseudo:
                            if (item.PseudoKind == PseudoNodeKind.Entry && entryNode != null)
                                AddToSelection(entryNode);
                            else if (item.PseudoKind == PseudoNodeKind.AnyState && anyStateNode != null)
                                AddToSelection(anyStateNode);
                            else if (item.PseudoKind == PseudoNodeKind.Exit && exitNode != null)
                                AddToSelection(exitNode);
                            break;
                    }
                }
            }
            finally
            {
                suppressSelectionSync = false;
            }
        }

        public bool IsReadOnly => readOnly;

        public void SetReadOnly(bool value)
        {
            if (readOnly == value)
                return;

            readOnly = value;
            ApplyReadOnlyState();
            UpdateEmptyHint();
        }

        public override EventPropagation DeleteSelection()
        {
            if (readOnly)
                return EventPropagation.Stop;

            if (commands != null && session != null)
            {
                GraphEditorSelection next = BuildSelectionFromCanvas();
                if (!next.Equals(session.Selection))
                    commands.Navigation.Select(next, true);

                commands.Authoring.DeleteSelection();
                return EventPropagation.Stop;
            }

            return base.DeleteSelection();
        }

        private void ApplyReadOnlyState()
        {
            foreach (StateGraphNode node in stateNodesById.Values)
            {
                node.capabilities = readOnly
                    ? Capabilities.Selectable
                    : Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable;

                SetPortReadOnly(node.InputPort);
                SetPortReadOnly(node.OutputPort);
            }

            ApplyPseudoReadOnly(entryNode);
            ApplyPseudoReadOnly(anyStateNode);
            ApplyPseudoReadOnly(exitNode);

            foreach (Edge edge in edges.ToList())
            {
                if (!(edge is TransitionEdge transitionEdge))
                    continue;

                transitionEdge.capabilities = readOnly || transitionEdge.IsEntryEdge
                    ? Capabilities.Selectable
                    : Capabilities.Selectable | Capabilities.Deletable;
            }
        }

        private void ApplyPseudoReadOnly(PseudoGraphNode node)
        {
            if (node == null)
                return;

            node.capabilities = readOnly
                ? Capabilities.Selectable
                : Capabilities.Selectable | Capabilities.Movable;

            SetPortReadOnly(node.InputPort);
            SetPortReadOnly(node.OutputPort);
        }

        private void SetPortReadOnly(Port port)
        {
            if (port == null)
                return;

            port.pickingMode = readOnly ? PickingMode.Ignore : PickingMode.Position;
            port.style.opacity = readOnly ? 0.4f : 1f;
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
            entryNode.SetPosition(new Rect(machine.entryPosition, PseudoNodeSize));
            anyStateNode.SetPosition(new Rect(machine.anyStatePosition, PseudoNodeSize));
            AddElement(entryNode);
            AddElement(anyStateNode);

            if (!isRoot)
            {
                exitNode = new PseudoGraphNode(PseudoNodeKind.Exit, machineId, this, true);
                exitNode.SetPosition(new Rect(machine.exitPosition, PseudoNodeSize));
                AddElement(exitNode);
            }

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
                    inputPort = exitNode != null ? exitNode.InputPort : null;
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
            ApplyReadOnlyState();
            UpdateEmptyHint();
            suppressChanges = false;
        }

        private void UpdateEmptyHint()
        {
            if (emptyHint == null)
                return;

            bool empty = Controller != null
                && Controller.Model != null
                && Controller.FindMachine(CurrentMachineId) != null
                && stateNodesById.Count == 0;
            emptyHint.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            emptyHint.text = readOnly
                ? "This machine has no states."
                : "This machine is empty. Use + Add in the mode bar or right-click the canvas.";
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
                evt.menu.AppendAction("Center on Selection", _ => FrameSelectionSafe());

                if (!readOnly)
                {
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
                }
            }

            if (!readOnly)
            {
                if (commands != null && evt.target is StateGraphNode stateTarget)
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Make Transition", _ =>
                    {
                        commands.Authoring.BeginLink();
                        commands.Authoring.SetLinkSource(stateTarget.StateId);
                    });
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction(
                    "Delete",
                    _ => DeleteSelection(),
                    HasDeletableSelection()
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
            }
        }

        public string CreateStateAt(Vector2 position)
        {
            if (readOnly)
                return null;

            if (commands != null)
            {
                GraphCommandResult result = commands.Authoring.CreateState(position);
                return result.Success ? result.Value : null;
            }

            if (Controller == null || string.IsNullOrEmpty(CurrentMachineId))
                return null;

            string stateId = Controller.AddState(CurrentMachineId, position);
            SelectCreatedNode(stateId);
            return stateId;
        }

        public string CreateSubMachineAt(Vector2 position)
        {
            if (readOnly)
                return null;

            if (commands != null)
            {
                GraphCommandResult result = commands.Authoring.CreateSubMachine(position);
                return result.Success ? result.Value : null;
            }

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
            if (readOnly || Controller == null || string.IsNullOrEmpty(CurrentMachineId)
                || outputPort == null || inputPort == null)
            {
                return;
            }

            Node fromNode = outputPort.node;
            Node toNode = inputPort.node;
            if (fromNode == null || toNode == null)
                return;

            if (commands != null && session != null)
            {
                GraphConnectSourceKind sourceKind;
                string sourceStateId;

                if (fromNode is StateGraphNode commandFromState)
                {
                    sourceKind = GraphConnectSourceKind.State;
                    sourceStateId = commandFromState.StateId;
                }
                else if (fromNode is PseudoGraphNode any && any.Kind == PseudoNodeKind.AnyState)
                {
                    sourceKind = GraphConnectSourceKind.AnyState;
                    sourceStateId = string.Empty;
                }
                else if (fromNode is PseudoGraphNode entrySource
                    && entrySource.Kind == PseudoNodeKind.Entry)
                {
                    sourceKind = GraphConnectSourceKind.Entry;
                    sourceStateId = string.Empty;
                }
                else
                {
                    return;
                }

                GraphConnectTargetKind targetKind;
                string targetStateId;

                if (toNode is StateGraphNode commandToState)
                {
                    targetKind = GraphConnectTargetKind.State;
                    targetStateId = commandToState.StateId;
                }
                else if (toNode is PseudoGraphNode exitTarget
                    && exitTarget.Kind == PseudoNodeKind.Exit
                    && exitTarget.ExitEnabled)
                {
                    targetKind = GraphConnectTargetKind.Exit;
                    targetStateId = string.Empty;
                }
                else
                {
                    return;
                }

                GraphCommandResult result = commands.Authoring.Connect(
                    new GraphConnectionRequest(sourceKind, sourceStateId, targetKind, targetStateId));

                if (!result.Success)
                    RefreshGraph();

                return;
            }

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
            if (readOnly || startPort == null)
                return compatible;

            ports.ForEach(port =>
            {
                if (port == null || port == startPort || port.direction == startPort.direction)
                    return;

                Node startNode = startPort.node;
                Node endNode = port.node;
                if (startNode == null || endNode == null)
                    return;

                // Entry only connects to a state (never directly to Exit).
                if (startNode is PseudoGraphNode startPseudo
                    && startPseudo.Kind == PseudoNodeKind.Entry
                    && !(endNode is StateGraphNode))
                {
                    return;
                }

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

        void IGraphEditorCanvasHost.FrameAll() => FrameAll();

        void IGraphEditorCanvasHost.FrameSelection() => FrameSelectionSafe();

        void IGraphEditorCanvasHost.ResetZoom() => ResetZoom();

        void IGraphEditorCanvasHost.SetGridVisible(bool visible) => SetGridVisible(visible);

        Vector2 IGraphEditorCanvasHost.GetDefaultCreatePosition()
        {
            Vector2 center = worldBound.center;
            return contentViewContainer.WorldToLocal(center) - StateNodeSize * 0.5f;
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
            linkSourceNode = null;

            graphViewChanged = OnGraphViewChanged;
            UpdateEmptyHint();
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

            if (readOnly)
            {
                change.elementsToRemove = null;
                return change;
            }

            if (commands != null && session != null)
            {
                if (change.movedElements != null)
                {
                    for (int i = 0; i < change.movedElements.Count; i++)
                    {
                        GraphElement element = change.movedElements[i];
                        if (element is StateGraphNode stateNode)
                            commands.Authoring.MoveState(stateNode.StateId, stateNode.GetPosition().position);
                        else if (element is PseudoGraphNode pseudoNode)
                            commands.Authoring.MovePseudoNode(pseudoNode.Kind, pseudoNode.GetPosition().position);
                    }
                }

                if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
                {
                    var items = new List<GraphEditorSelectionItem>();
                    for (int i = 0; i < change.elementsToRemove.Count; i++)
                    {
                        GraphElement element = change.elementsToRemove[i];
                        if (element is StateGraphNode removedState)
                            items.Add(new GraphEditorSelectionItem(
                                GraphEditorSelectionItemKind.State,
                                removedState.StateId));
                        else if (element is TransitionEdge removedEdge
                            && !removedEdge.IsEntryEdge
                            && !string.IsNullOrEmpty(removedEdge.TransitionId))
                            items.Add(new GraphEditorSelectionItem(
                                GraphEditorSelectionItemKind.Transition,
                                removedEdge.TransitionId));
                    }

                    change.elementsToRemove = null;

                    if (items.Count > 0)
                    {
                        GraphEditorSelection next = GraphEditorSelection.Multi(items);
                        EditorApplication.delayCall += () =>
                        {
                            if (commands == null || session == null)
                                return;

                            if (!next.Equals(session.Selection))
                                commands.Navigation.Select(next, true);

                            commands.Authoring.DeleteSelection();
                        };
                    }
                }

                return change;
            }

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
            if (commands != null)
            {
                commands.Navigation.OpenSubMachine(hostStateId);
                return;
            }

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
