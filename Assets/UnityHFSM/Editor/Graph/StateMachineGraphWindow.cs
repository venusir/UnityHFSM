using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class StateMachineGraphWindow : EditorWindow
    {
        [SerializeField] private StateMachineGraphAsset asset;
        [SerializeField] private StateMachineRunner followedRunner;
        [SerializeField] private bool selectionLocked;
        private GraphEditorController controller;
        private StateMachineGraphView graphView;
        private BlackboardPanel blackboardPanel;
        private InspectorPanel inspectorPanel;
        private ValidationPanel validationPanel;
        private RuntimePanel runtimePanel;
        private Toggle lockToggle;
        private Label followStatus;
        private VisualElement breadcrumb;

        private string currentMachineId;
        private string lastBreadcrumbKey;
        private ISelectable lastSelection;
        private double nextRuntimePoll;

        public StateMachineGraphAsset Asset => asset;
        public GraphEditorController Controller => controller;
        public string CurrentMachineId => currentMachineId;

        [MenuItem("Tools/UnityHFSM/State Machine Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<StateMachineGraphWindow>("UnityHFSM Graph");
            window.RestoreOrFollowSelection();
            window.Show();
        }

        public static void OpenAsset(StateMachineGraphAsset graphAsset)
        {
            var window = GetWindow<StateMachineGraphWindow>("UnityHFSM Graph");
            window.LoadAsset(graphAsset);
            window.Show();
        }

        private void OnEnable()
        {
            BuildUI();
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                controller.Changed += OnControllerChanged;
            }

            RestoreOrFollowSelection();
            RefreshAll();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (controller != null)
                controller.Changed -= OnControllerChanged;
        }

        private void BuildUI()
        {
            if (rootVisualElement.childCount > 0)
                return;

            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1f;
            rootVisualElement.Add(main);

            blackboardPanel = new BlackboardPanel(this);
            main.Add(blackboardPanel);

            var graphColumn = new VisualElement();
            graphColumn.style.flexDirection = FlexDirection.Column;
            graphColumn.style.flexGrow = 1f;
            graphColumn.style.minWidth = 0f;
            main.Add(graphColumn);

            var graphHeader = GraphEditorStyles.CreateHeaderBand(
                out breadcrumb,
                out VisualElement graphHeaderRight);
            graphColumn.Add(graphHeader);

            followStatus = GraphEditorStyles.CreateStatusLabel();
            followStatus.style.display = DisplayStyle.None;
            graphHeaderRight.Add(followStatus);

            lockToggle = new Toggle("Lock") { value = selectionLocked };
            lockToggle.tooltip = "Keep the current graph when the selection changes.";
            lockToggle.style.height = 18f;
            lockToggle.style.marginLeft = 4f;
            lockToggle.style.marginRight = 0f;
            lockToggle.style.marginTop = 0f;
            lockToggle.style.marginBottom = 0f;
            lockToggle.style.flexShrink = 0f;
            lockToggle.RegisterValueChangedCallback(evt =>
            {
                selectionLocked = evt.newValue;
                if (selectionLocked)
                {
                    UpdateFollowStatus();
                }
                else
                {
                    TryFollowSelection();
                }
            });
            graphHeaderRight.Add(lockToggle);

            graphView = new StateMachineGraphView();
            graphView.style.flexGrow = 1f;
            graphView.OpenSubMachineRequested = SetCurrentMachine;
            graphColumn.Add(graphView);

            inspectorPanel = new InspectorPanel(this);
            main.Add(inspectorPanel);

            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.minHeight = 210f;
            bottom.style.maxHeight = 260f;
            GraphEditorStyles.SetBorder(bottom, GraphEditorStyles.SeparatorColor, topWidth: 1f);
            rootVisualElement.Add(bottom);

            validationPanel = new ValidationPanel(this);
            validationPanel.style.width = 430f;
            bottom.Add(validationPanel);

            runtimePanel = new RuntimePanel(this);
            runtimePanel.style.flexGrow = 1f;
            bottom.Add(runtimePanel);
        }

        private void LoadAsset(StateMachineGraphAsset value, StateMachineRunner sourceRunner = null)
        {
            if (value == asset && controller != null)
            {
                if (sourceRunner != null)
                    followedRunner = sourceRunner;

                RefreshAll();

                if (followedRunner != null)
                    runtimePanel?.BindRunner(followedRunner);

                UpdateFollowStatus();
                return;
            }

            if (controller != null)
                controller.Changed -= OnControllerChanged;

            asset = value;
            followedRunner = sourceRunner;
            controller = asset != null ? new GraphEditorController(asset) : null;

            graphView.Controller = controller;
            graphView.OpenSubMachineRequested = SetCurrentMachine;

            if (controller != null)
            {
                controller.InitializeIfEmpty();
                controller.Changed += OnControllerChanged;
                currentMachineId = asset.Graph.rootMachineId;
            }
            else
            {
                currentMachineId = null;
            }

            RefreshAll();

            if (followedRunner != null)
                runtimePanel?.BindRunner(followedRunner);

            UpdateFollowStatus();
            graphView.FrameGraph();
        }

        private void RefreshAll()
        {
            if (controller == null || asset == null)
            {
                graphView.Controller = null;
                graphView.LoadMachine(null);
                blackboardPanel.Refresh();
                inspectorPanel.ShowMachine();
                validationPanel.Refresh();
                runtimePanel.RefreshRunnerList();
                UpdateBreadcrumb();
                return;
            }

            if (controller.FindMachine(currentMachineId) == null)
                currentMachineId = asset.Graph.rootMachineId;

            graphView.Controller = controller;
            graphView.LoadMachine(currentMachineId);
            blackboardPanel.Refresh();
            inspectorPanel.Refresh();
            validationPanel.Refresh();
            runtimePanel.RefreshRunnerList();
            UpdateBreadcrumb();
        }

        public void SetCurrentMachine(string machineId)
        {
            if (controller == null || string.IsNullOrEmpty(machineId))
                return;

            if (controller.FindMachine(machineId) == null)
                return;

            currentMachineId = machineId;
            graphView.LoadMachine(machineId);
            graphView.FrameGraph();
            inspectorPanel.ShowMachine();
            UpdateBreadcrumb();
        }

        private void OnEditorUpdate()
        {
            if (graphView != null)
            {
                ISelectable selected = graphView.selection.Count > 0
                    ? graphView.selection[0]
                    : null;

                if (!ReferenceEquals(selected, lastSelection))
                {
                    lastSelection = selected;
                    HandleSelection(selected);
                }
            }

            if (runtimePanel != null && EditorApplication.timeSinceStartup >= nextRuntimePoll)
            {
                nextRuntimePoll = EditorApplication.timeSinceStartup + 0.1d;
                runtimePanel.UpdateLive();
                graphView.ApplySnapshot(runtimePanel.CurrentSnapshot);
            }
        }

        private void HandleSelection(ISelectable selected)
        {
            if (selected is StateGraphNode stateNode)
            {
                inspectorPanel.ShowState(stateNode.StateId);
                return;
            }

            if (selected is TransitionEdge edge)
            {
                if (edge.IsEntryEdge || string.IsNullOrEmpty(edge.TransitionId))
                    inspectorPanel.ShowMachine();
                else
                    inspectorPanel.ShowTransition(edge.TransitionId);

                return;
            }

            inspectorPanel.ShowMachine();
        }

        private void UpdateBreadcrumb()
        {
            if (breadcrumb == null)
                return;

            var chain = new List<MachineDef>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (controller != null)
            {
                MachineDef machine = controller.FindMachine(currentMachineId);
                while (machine != null && seen.Add(machine.id))
                {
                    chain.Add(machine);

                    if (string.IsNullOrEmpty(machine.parentStateId))
                        break;

                    StateDef host = controller.FindState(machine.parentStateId);
                    machine = host != null ? controller.FindMachine(host.machineId) : null;
                }
            }

            chain.Reverse();
            var keyBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < chain.Count; i++)
                keyBuilder.Append(chain[i].id).Append(':').Append(chain[i].name).Append('|');

            string key = keyBuilder.ToString();
            if (key == lastBreadcrumbKey)
                return;

            lastBreadcrumbKey = key;
            breadcrumb.Clear();

            if (chain.Count == 0)
            {
                breadcrumb.Add(GraphEditorStyles.CreateMutedLabel("No machine selected."));
                return;
            }

            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                    breadcrumb.Add(GraphEditorStyles.CreateBreadcrumbSeparator());

                MachineDef machine = chain[i];
                string machineId = machine.id;
                string displayName = string.IsNullOrEmpty(machine.name) ? "(unnamed)" : machine.name;
                Button button = GraphEditorStyles.CreateBreadcrumbButton(
                    displayName,
                    () => SetCurrentMachine(machineId));
                button.tooltip = machine.name;

                if (i == chain.Count - 1)
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                breadcrumb.Add(button);
            }
        }

        private void OnControllerChanged()
        {
            RefreshAll();
        }

        private void OnUndoRedo()
        {
            lastBreadcrumbKey = null;
            RefreshAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            runtimePanel?.RefreshRunnerList();
            graphView?.ClearRuntimeHighlight();
        }

        private void RestoreOrFollowSelection()
        {
            if (selectionLocked)
            {
                if (asset != null)
                    LoadAsset(asset, followedRunner);
                else
                    UpdateFollowStatus();

                return;
            }

            if (asset != null && controller == null)
                LoadAsset(asset, followedRunner);

            TryFollowSelection();
        }

        private void OnSelectionChange()
        {
            if (graphView == null)
                return;

            if (selectionLocked)
            {
                UpdateFollowStatus();
                return;
            }

            TryFollowSelection();
        }

        private void TryFollowSelection()
        {
            if (Selection.activeObject is StateMachineGraphAsset selectedAsset)
            {
                LoadAsset(selectedAsset);
                return;
            }

            StateMachineRunner runner = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<StateMachineRunner>()
                : null;

            if (runner == null)
            {
                SetFollowStatus("No StateMachineRunner selected.");
                return;
            }

            if (runner.Graph == null)
            {
                SetFollowStatus("Selected Runner has no graph.");
                return;
            }

            LoadAsset(runner.Graph, runner);
        }

        private void UpdateFollowStatus()
        {
            if (selectionLocked)
            {
                SetFollowStatus(asset != null ? "Locked: " + asset.name : "Locked.");
                return;
            }

            if (asset == null)
            {
                SetFollowStatus("No graph asset selected.");
                return;
            }

            if (followedRunner != null)
            {
                SetFollowStatus("Following: " + followedRunner.gameObject.name);
                return;
            }

            SetFollowStatus("Graph: " + asset.name);
        }

        private void SetFollowStatus(string text)
        {
            if (followStatus == null)
                return;

            followStatus.text = text;
            followStatus.tooltip = text;
            followStatus.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
