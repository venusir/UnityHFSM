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
        private readonly Dictionary<string, StateGraphNode> stateNodesById =
            new Dictionary<string, StateGraphNode>(StringComparer.Ordinal);

        private PseudoGraphNode entryNode;
        private PseudoGraphNode anyStateNode;
        private PseudoGraphNode exitNode;
        private bool suppressChanges;

        public GraphEditorController Controller { get; set; }
        public string CurrentMachineId { get; private set; }
        public GraphEdgeConnectorListener EdgeConnectorListener { get; }
        public Action<string> OpenSubMachineRequested { get; set; }

        public StateMachineGraphView()
        {
            EdgeConnectorListener = new GraphEdgeConnectorListener(this);
            style.flexGrow = 1f;

            var grid = new GridBackground();
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

            entryNode.SetPosition(new Rect(machine.entryPosition, new Vector2(130f, 60f)));
            anyStateNode.SetPosition(new Rect(machine.anyStatePosition, new Vector2(130f, 60f)));
            exitNode.SetPosition(new Rect(machine.exitPosition, new Vector2(130f, 60f)));

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

                node.SetPosition(new Rect(state.position, new Vector2(180f, 100f)));
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
                    new Color(0.25f, 0.75f, 0.35f));
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

                AddTransitionEdge(outputPort, inputPort, transition.id, false, color);
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
            bool exitPending = false;

            for (int i = 0; i < snapshot.machines.Count; i++)
            {
                MachineRuntimeSnapshot machine = snapshot.machines[i];
                if (machine.machineId == CurrentMachineId)
                {
                    pendingStateId = machine.pendingStateId;
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
        }

        public void ClearRuntimeHighlight()
        {
            foreach (StateGraphNode node in stateNodesById.Values)
                SetHighlight(node, false, false);

            if (entryNode != null) SetHighlight(entryNode, false, false);
            if (anyStateNode != null) SetHighlight(anyStateNode, false, false);
            if (exitNode != null) SetHighlight(exitNode, false, false);
        }

        private void ClearGraph()
        {
            graphViewChanged = null;

            foreach (Edge edge in edges.ToList())
                RemoveElement(edge);

            foreach (Node node in nodes.ToList())
                RemoveElement(node);

            stateNodesById.Clear();
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
            Color color)
        {
            if (outputPort == null || inputPort == null)
                return;

            var edge = new TransitionEdge
            {
                output = outputPort,
                input = inputPort,
                TransitionId = transitionId,
                IsEntryEdge = isEntry,
            };

            if (isEntry)
                edge.capabilities &= ~Capabilities.Deletable;

            AddElement(edge);
            edge.SetColor(color);
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
