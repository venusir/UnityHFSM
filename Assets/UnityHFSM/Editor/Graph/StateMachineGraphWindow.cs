using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class StateMachineGraphWindow : EditorWindow
    {
        private StateMachineGraphAsset asset;
        private GraphEditorController controller;
        private StateMachineGraphView graphView;
        private BlackboardPanel blackboardPanel;
        private InspectorPanel inspectorPanel;
        private ValidationPanel validationPanel;
        private RuntimePanel runtimePanel;
        private ObjectField assetField;
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
            window.TryUseSelection();
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

            TryUseSelection();
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

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.paddingLeft = 6f;
            toolbar.style.paddingRight = 6f;
            toolbar.style.paddingTop = 4f;
            toolbar.style.paddingBottom = 4f;
            rootVisualElement.Add(toolbar);

            assetField = new ObjectField("Graph")
            {
                objectType = typeof(StateMachineGraphAsset),
                allowSceneObjects = false,
            };
            assetField.style.width = 280f;
            assetField.RegisterValueChangedCallback(evt =>
                LoadAsset(evt.newValue as StateMachineGraphAsset));
            toolbar.Add(assetField);

            toolbar.Add(new Button(AddState) { text = "Add State" });
            toolbar.Add(new Button(AddSubMachine) { text = "Add Sub-State Machine" });
            toolbar.Add(new Button(() => graphView.FrameGraph()) { text = "Frame" });
            toolbar.Add(new Button(Save) { text = "Save" });
            toolbar.Add(new Button(Validate) { text = "Validate" });
            toolbar.Add(new Button(ExportJson) { text = "Export JSON" });
            toolbar.Add(new Button(ImportJson) { text = "Import JSON" });

            breadcrumb = new VisualElement();
            breadcrumb.style.flexDirection = FlexDirection.Row;
            breadcrumb.style.flexWrap = Wrap.Wrap;
            breadcrumb.style.paddingLeft = 8f;
            breadcrumb.style.paddingRight = 8f;
            breadcrumb.style.paddingBottom = 4f;
            rootVisualElement.Add(breadcrumb);

            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1f;
            rootVisualElement.Add(main);

            blackboardPanel = new BlackboardPanel(this);
            main.Add(blackboardPanel);

            graphView = new StateMachineGraphView();
            graphView.style.flexGrow = 1f;
            graphView.OpenSubMachineRequested = SetCurrentMachine;
            main.Add(graphView);

            inspectorPanel = new InspectorPanel(this);
            main.Add(inspectorPanel);

            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.minHeight = 190f;
            bottom.style.maxHeight = 230f;
            rootVisualElement.Add(bottom);

            validationPanel = new ValidationPanel(this);
            validationPanel.style.width = 430f;
            bottom.Add(validationPanel);

            runtimePanel = new RuntimePanel(this);
            runtimePanel.style.flexGrow = 1f;
            bottom.Add(runtimePanel);
        }

        private void LoadAsset(StateMachineGraphAsset value)
        {
            if (value == asset)
            {
                RefreshAll();
                return;
            }

            if (controller != null)
                controller.Changed -= OnControllerChanged;

            asset = value;
            controller = asset != null ? new GraphEditorController(asset) : null;
            assetField?.SetValueWithoutNotify(asset);

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

        private void AddState()
        {
            if (controller == null || string.IsNullOrEmpty(currentMachineId))
                return;

            int count = CountStates(currentMachineId);
            var position = new Vector2(120f + (count % 4) * 210f, 70f + (count / 4) * 130f);
            string stateId = controller.AddState(currentMachineId, position);
            if (!string.IsNullOrEmpty(stateId))
                inspectorPanel.ShowState(stateId);
        }

        private void AddSubMachine()
        {
            if (controller == null || string.IsNullOrEmpty(currentMachineId))
                return;

            int count = CountStates(currentMachineId);
            var position = new Vector2(120f + (count % 4) * 210f, 70f + (count / 4) * 130f);
            string stateId = controller.AddSubMachine(currentMachineId, position);
            if (!string.IsNullOrEmpty(stateId))
                inspectorPanel.ShowState(stateId);
        }

        private int CountStates(string machineId)
        {
            int count = 0;
            GraphModel model = controller.Model;

            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state != null && state.machineId == machineId)
                    count++;
            }

            return count;
        }

        private void Save()
        {
            if (asset == null)
                return;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private void Validate()
        {
            validationPanel.Refresh();
        }

        private void ExportJson()
        {
            if (asset != null)
                GraphJsonEditorIO.Export(asset);
        }

        private void ImportJson()
        {
            if (asset == null)
                return;

            if (GraphJsonEditorIO.Import(asset))
                RefreshAll();
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
                breadcrumb.Add(new Label("No machine selected."));
                return;
            }

            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                    breadcrumb.Add(new Label(">"));

                MachineDef machine = chain[i];
                string machineId = machine.id;
                var button = new Button(() => SetCurrentMachine(machineId))
                {
                    text = string.IsNullOrEmpty(machine.name) ? "(unnamed)" : machine.name,
                };

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

        private void TryUseSelection()
        {
            if (Selection.activeObject is StateMachineGraphAsset selectedAsset)
                LoadAsset(selectedAsset);
        }
    }
}
