using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    [Serializable]
    internal sealed class GraphEditorLayoutState
    {
        public float parametersWidth = 250f;
        public float inspectorWidth = 320f;
        public float drawerHeight = 220f;
        public bool parametersVisible = true;
        public bool inspectorVisible = true;
        public bool drawerVisible;
        public int drawerTab;
        public float previewSpeed = 1f;
    }

    public sealed class StateMachineGraphWindow : EditorWindow
    {
        [SerializeField] private StateMachineGraphAsset asset;
        [SerializeField] private StateMachineRunner followedRunner;
        [SerializeField] private bool selectionLocked;
        [SerializeField] private GraphEditorLayoutState layout = new GraphEditorLayoutState();

        private GraphEditorController controller;
        private StateMachineGraphView graphView;
        private GraphToolbar toolbar;
        private BlackboardPanel blackboardPanel;
        private InspectorPanel inspectorPanel;
        private BottomDrawer bottomDrawer;
        private RuntimeLogPanel runtimeLogPanel;
        private ResizableSplitter parametersSplitter;
        private ResizableSplitter inspectorSplitter;
        private ResizableSplitter drawerSplitter;
        private VisualElement workspace;

        private GraphPreviewSession preview;
        private RuntimeSession runtimeSession;
        private GraphSearchProvider searchProvider;

        private GraphValidationResult validationResult;
        private GraphRuntimeSnapshot currentSnapshot;
        private GraphRuntime lastRuntimeRef;
        private GameObject previewHost;
        private string currentMachineId;

        private ISelectable lastSelection;
        private bool previewStale;
        private bool validationDirty;
        private bool layoutClamping;
        private bool registryRefreshQueued;
        private double nextRuntimePoll;
        private double nextTargetRefresh;
        private double nextValidationRefresh;

        public StateMachineGraphAsset Asset => asset;
        public GraphEditorController Controller => controller;
        public string CurrentMachineId => currentMachineId;

        public GraphRuntime CurrentRuntime =>
            runtimeSession != null ? runtimeSession.CurrentRuntime : null;

        public GraphRuntimeSnapshot CurrentSnapshot => currentSnapshot;
        public bool IsPreviewing => preview != null && preview.IsRunning;
        public string PreviewLastError => preview != null ? preview.LastError : null;
        public GameObject PreviewHost => preview != null ? preview.Host : previewHost;
        public bool IsGridVisible => graphView != null && graphView.IsGridVisible;
        public bool IsParametersVisible => layout.parametersVisible;
        public bool IsInspectorVisible => layout.inspectorVisible;
        public bool IsRuntimeLogVisible =>
            layout.drawerVisible
            && bottomDrawer != null
            && bottomDrawer.CurrentTab == BottomDrawer.Tab.RuntimeLog;

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
            if (layout == null)
                layout = new GraphEditorLayoutState();

            layout.drawerTab = Mathf.Clamp(layout.drawerTab, 0, 1);
            layout.previewSpeed = Mathf.Clamp(layout.previewSpeed, 0.1f, 4f);
            previewHost = null;

            minSize = new Vector2(840f, 480f);
            BuildUI();

            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            GraphRuntimeRegistry.Changed += OnRuntimeRegistryChanged;

            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                controller.Changed += OnControllerChanged;
                controller.Dirty -= OnControllerDirty;
                controller.Dirty += OnControllerDirty;
            }

            RestoreOrFollowSelection();
            RefreshAll();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            GraphRuntimeRegistry.Changed -= OnRuntimeRegistryChanged;

            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                controller.Dirty -= OnControllerDirty;
            }

            StopPreview();
            runtimeSession?.Dispose();
            runtimeSession = null;
            preview?.Dispose();
            preview = null;
            DestroySearchProvider();
            lastRuntimeRef = null;
        }

        private void OnBeforeAssemblyReload()
        {
            StopPreview();
            runtimeSession?.Dispose();
            runtimeSession = null;
            preview?.Dispose();
            preview = null;
            DestroySearchProvider();
        }

        private void BuildUI()
        {
            if (rootVisualElement.childCount > 0)
                return;

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.focusable = true;

            toolbar = new GraphToolbar(this);
            rootVisualElement.Add(toolbar);

            workspace = new VisualElement();
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1f;
            workspace.style.minHeight = 0f;
            rootVisualElement.Add(workspace);

            blackboardPanel = new BlackboardPanel(this);
            workspace.Add(blackboardPanel);

            parametersSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Vertical,
                blackboardPanel,
                true,
                layout.parametersWidth,
                180f,
                MaxParametersWidth,
                size => layout.parametersWidth = size);
            workspace.Add(parametersSplitter);

            var graphColumn = new VisualElement();
            graphColumn.style.flexDirection = FlexDirection.Column;
            graphColumn.style.flexGrow = 1f;
            graphColumn.style.minWidth = 0f;
            workspace.Add(graphColumn);

            graphView = new StateMachineGraphView();
            graphView.style.flexGrow = 1f;
            graphView.focusable = true;
            graphView.OpenSubMachineRequested = SetCurrentMachine;
            graphColumn.Add(graphView);
            graphView.RegisterCallback<PointerDownEvent>(_ => rootVisualElement.Focus());

            inspectorPanel = new InspectorPanel(this);

            inspectorSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Vertical,
                inspectorPanel,
                false,
                layout.inspectorWidth,
                240f,
                MaxInspectorWidth,
                size => layout.inspectorWidth = size);
            workspace.Add(inspectorSplitter);
            workspace.Add(inspectorPanel);

            bottomDrawer = new BottomDrawer(this);
            bottomDrawer.TabChanged += tab => layout.drawerTab = (int)tab;
            bottomDrawer.CollapseRequested += CloseDrawer;
            bottomDrawer.ValidationPanel.IssueSelected += OpenIssue;
            runtimeLogPanel = bottomDrawer.RuntimeLogPanel;

            drawerSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Horizontal,
                bottomDrawer,
                false,
                layout.drawerHeight,
                120f,
                MaxDrawerHeight,
                size => layout.drawerHeight = size);
            rootVisualElement.Add(drawerSplitter);
            rootVisualElement.Add(bottomDrawer);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            workspace.RegisterCallback<GeometryChangedEvent>(_ => ClampLayout());
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ClampLayout());

            ApplyLayoutVisibility();
        }

        private void LoadAsset(StateMachineGraphAsset value, StateMachineRunner sourceRunner = null)
        {
            if (value == asset && controller != null)
            {
                if (sourceRunner != null)
                    followedRunner = sourceRunner;

                if (preview == null && asset != null)
                {
                    preview = new GraphPreviewSession(asset.Graph, previewHost, layout.previewSpeed);
                    runtimeSession = new RuntimeSession(preview);
                    runtimeSession.SetGraphId(asset.Graph.graphId);
                }

                if (searchProvider == null)
                    RebuildSearchProvider();

                RefreshAll();

                if (followedRunner != null && runtimeSession != null)
                    runtimeSession.RefreshLiveTargets();

                UpdateFollowStatus();
                RefreshRuntimeTargets();
                return;
            }

            DetachController();
            StopPreview();
            runtimeSession?.Dispose();
            runtimeSession = null;
            preview?.Dispose();
            preview = null;

            asset = value;
            followedRunner = sourceRunner;
            previewStale = false;
            currentSnapshot = null;
            lastRuntimeRef = null;
            lastSelection = null;
            validationDirty = false;

            controller = asset != null ? new GraphEditorController(asset) : null;
            graphView.Controller = controller;
            graphView.OpenSubMachineRequested = SetCurrentMachine;

            if (controller != null)
            {
                controller.InitializeIfEmpty();
                controller.Changed += OnControllerChanged;
                controller.Dirty += OnControllerDirty;
                currentMachineId = asset.Graph.rootMachineId;

                preview = new GraphPreviewSession(asset.Graph, previewHost, layout.previewSpeed);
                runtimeSession = new RuntimeSession(preview);
                runtimeSession.SetGraphId(asset.Graph.graphId);
                RebuildSearchProvider();
            }
            else
            {
                currentMachineId = null;
                DestroySearchProvider();
            }

            RefreshAll();
            UpdateFollowStatus();

            if (asset != null)
                graphView.FrameGraph();
        }

        private void DetachController()
        {
            if (controller == null)
                return;

            controller.Changed -= OnControllerChanged;
            controller.Dirty -= OnControllerDirty;
            controller = null;
        }

        private void RefreshAll()
        {
            if (controller == null || asset == null)
            {
                graphView.Controller = null;
                graphView.LoadMachine(null);
                lastSelection = null;
                blackboardPanel.Refresh();
                inspectorPanel.ShowMachine();
                validationResult = null;
                bottomDrawer?.SetValidation(null);
                toolbar?.SetValidation(null);
                runtimeSession?.RefreshLiveTargets();
                RefreshRuntimeTargets();
                ApplyRuntimeSnapshot();
                UpdateBreadcrumb();
                UpdateToolbar();
                return;
            }

            if (controller.FindMachine(currentMachineId) == null)
                currentMachineId = asset.Graph.rootMachineId;

            graphView.Controller = controller;
            graphView.LoadMachine(currentMachineId);
            lastSelection = null;
            blackboardPanel.Refresh();
            inspectorPanel.Refresh();
            Revalidate();
            runtimeSession?.RefreshLiveTargets();
            RefreshRuntimeTargets();
            ApplyRuntimeSnapshot();
            UpdateBreadcrumb();
            UpdateToolbar();
        }

        public void SetCurrentMachine(string machineId)
        {
            SetCurrentMachineInternal(machineId, true);
        }

        private void SetCurrentMachineInternal(string machineId, bool frame)
        {
            if (controller == null || string.IsNullOrEmpty(machineId))
                return;

            if (controller.FindMachine(machineId) == null)
                return;

            currentMachineId = machineId;
            graphView.LoadMachine(machineId);
            lastSelection = null;
            inspectorPanel.ShowMachine();
            UpdateBreadcrumb();
            ApplyRuntimeSnapshot();

            if (frame)
                graphView.FrameGraph();
        }

        public void NavigateTo(string machineId, string stateId, string transitionId)
        {
            if (controller == null)
                return;

            if (string.IsNullOrEmpty(machineId))
            {
                machineId = GraphNavigation.ResolveMachineIdForState(controller.Model, stateId);
                if (string.IsNullOrEmpty(machineId))
                    machineId = GraphNavigation.ResolveMachineIdForTransition(controller.Model, transitionId);
            }

            if (string.IsNullOrEmpty(machineId) || controller.FindMachine(machineId) == null)
                return;

            SetCurrentMachineInternal(machineId, false);

            bool selected = false;

            if (!string.IsNullOrEmpty(transitionId) && controller.FindTransition(transitionId) != null)
            {
                graphView.SelectTransition(transitionId);
                selected = true;
            }
            else if (!string.IsNullOrEmpty(stateId) && controller.FindState(stateId) != null)
            {
                graphView.SelectState(stateId);
                selected = true;
            }

            if (!selected)
                graphView.FrameGraph();

            if (graphView.selection.Count > 0)
            {
                lastSelection = graphView.selection[0];
                HandleSelection(lastSelection);
            }
        }

        private void OpenIssue(GraphIssue issue)
        {
            if (issue == null || controller == null)
                return;

            string machineId = GraphNavigation.ResolveMachineIdForIssue(controller.Model, issue);
            if (string.IsNullOrEmpty(machineId))
                return;

            NavigateTo(machineId, issue.stateId, issue.transitionId);
        }

        public void NavigateToTransition(TransitionRecord record)
        {
            if (record == null)
                return;

            string stateId = !string.IsNullOrEmpty(record.toStateId)
                ? record.toStateId
                : record.fromStateId;
            NavigateTo(record.machineId, stateId, record.transitionId);
        }

        public string BuildActivePath(GraphRuntimeSnapshot snapshot)
        {
            if (snapshot == null || snapshot.activePathStateIds.Count == 0)
                return "(none)";

            var builder = new StringBuilder();

            for (int i = 0; i < snapshot.activePathStateIds.Count; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                string id = snapshot.activePathStateIds[i];
                StateDef state = controller != null ? controller.FindState(id) : null;
                builder.Append(state != null ? state.name : id);
            }

            return builder.ToString();
        }

        private void UpdateBreadcrumb()
        {
            if (toolbar == null)
                return;

            List<MachineDef> chain = GraphNavigation.BuildMachineChain(
                controller != null ? controller.Model : null,
                currentMachineId);
            toolbar.UpdateBreadcrumb(chain, currentMachineId);
        }

        private void OnEditorUpdate()
        {
            if (graphView == null)
                return;

            if (preview != null && preview.IsRunning)
                preview.Advance(EditorApplication.timeSinceStartup);

            ISelectable selected = graphView.selection.Count > 0
                ? graphView.selection[0]
                : null;

            if (!ReferenceEquals(selected, lastSelection))
            {
                lastSelection = selected;
                HandleSelection(selected);
            }

            double now = EditorApplication.timeSinceStartup;

            if (validationDirty && now >= nextValidationRefresh)
                Revalidate();

            if (runtimeSession != null && now >= nextRuntimePoll)
            {
                nextRuntimePoll = now + 0.1d;
                runtimeSession.Poll();
                ApplyRuntimeSnapshot();
                UpdateToolbar();
            }

            if (Application.isPlaying && now >= nextTargetRefresh)
            {
                nextTargetRefresh = now + 1d;
                runtimeSession?.RefreshLiveTargets();
                RefreshRuntimeTargets();
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

        private void ApplyRuntimeSnapshot()
        {
            GraphRuntime runtime = CurrentRuntime;
            GraphRuntimeSnapshot snapshot = runtimeSession != null ? runtimeSession.Snapshot : null;
            currentSnapshot = snapshot;

            if (!ReferenceEquals(runtime, lastRuntimeRef))
            {
                lastRuntimeRef = runtime;
                blackboardPanel?.Refresh();
                inspectorPanel?.Refresh();
            }

            if (snapshot == null)
            {
                graphView?.ClearRuntimeHighlight();
                runtimeLogPanel?.UpdateLive(null, runtime);
                return;
            }

            graphView?.ApplySnapshot(snapshot);
            blackboardPanel?.UpdateLive(snapshot);
            inspectorPanel?.UpdateLive(snapshot);
            runtimeLogPanel?.UpdateLive(snapshot, runtime);
        }

        private void Revalidate()
        {
            validationDirty = false;

            if (controller == null || controller.Model == null)
            {
                validationResult = null;
            }
            else
            {
                validationResult = GraphValidator.Validate(controller.Model);
            }

            bottomDrawer?.SetValidation(validationResult);
            toolbar?.SetValidation(validationResult);
            graphView?.RefreshDecorations();

            if (preview != null && preview.IsRunning && asset != null)
                previewStale = GraphModelFingerprint.Compute(asset.Graph) != preview.Fingerprint;

            UpdateToolbar();
        }

        private void MarkDirtyDebounced()
        {
            validationDirty = true;
            nextValidationRefresh = EditorApplication.timeSinceStartup + 0.2d;
        }

        private void OnControllerChanged()
        {
            RefreshAll();
        }

        private void OnControllerDirty()
        {
            MarkDirtyDebounced();
        }

        private void OnUndoRedo()
        {
            RefreshAll();
        }

        private void OnRuntimeRegistryChanged()
        {
            if (registryRefreshQueued)
                return;

            registryRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                registryRefreshQueued = false;

                if (this == null || runtimeSession == null)
                    return;

                runtimeSession.RefreshLiveTargets();
                runtimeSession.Poll();
                RefreshRuntimeTargets();
                ApplyRuntimeSnapshot();
                UpdateToolbar();
            };
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                StopPreview();

            if (state == PlayModeStateChange.EnteredPlayMode
                || state == PlayModeStateChange.EnteredEditMode)
            {
                lastRuntimeRef = null;
                currentSnapshot = null;
                graphView?.ClearRuntimeHighlight();
                runtimeSession?.RefreshLiveTargets();
                runtimeSession?.Poll();
                RefreshRuntimeTargets();
                ApplyRuntimeSnapshot();
                UpdateToolbar();
            }
        }

        public void TogglePreviewPlayback()
        {
            if (Application.isPlaying)
            {
                runtimeSession?.TogglePause();
                runtimeSession?.Poll();
                ApplyRuntimeSnapshot();
                UpdateToolbar();
                return;
            }

            if (preview == null)
                return;

            if (preview.IsRunning)
            {
                preview.TogglePause();
            }
            else
            {
                bool started = preview.Start();
                if (!started)
                    OpenValidationTab();
                else
                    previewStale = false;
            }

            runtimeSession?.Poll();
            ApplyRuntimeSnapshot();
            UpdateToolbar();
        }

        public void StepRuntime()
        {
            if (Application.isPlaying)
                runtimeSession?.Step();
            else
                preview?.Step();

            runtimeSession?.Poll();
            ApplyRuntimeSnapshot();
            UpdateToolbar();
        }

        public void RestartRuntime()
        {
            if (!Application.isPlaying && preview != null)
            {
                bool restarted = preview.Restart(true);
                previewStale = !restarted;
                if (!restarted)
                    OpenValidationTab();
            }
            else
            {
                runtimeSession?.Restart();
            }

            runtimeSession?.Poll();
            ApplyRuntimeSnapshot();
            UpdateToolbar();
        }

        public void SetPreviewSpeed(float speed)
        {
            layout.previewSpeed = speed;

            if (preview != null)
                preview.Speed = speed;

            UpdateToolbar();
        }

        public void SetPreviewHost(GameObject host)
        {
            previewHost = host;

            if (preview != null)
                preview.Host = host;

            runtimeSession?.Poll();
            ApplyRuntimeSnapshot();
            UpdateToolbar();
        }

        public void SelectRuntimeTarget(int index)
        {
            if (runtimeSession == null)
                return;

            runtimeSession.SelectTarget(index);
            runtimeSession.Poll();
            RefreshRuntimeTargets();
            ApplyRuntimeSnapshot();
            UpdateToolbar();
        }

        public void SetSelectionLocked(bool value)
        {
            selectionLocked = value;

            if (!selectionLocked)
                TryFollowSelection();

            UpdateFollowStatus();
        }

        private void StopPreview()
        {
            if (preview == null || !preview.IsRunning)
                return;

            preview.Stop();
            previewStale = false;
            currentSnapshot = null;
            runtimeSession?.Poll();
            graphView?.ClearRuntimeHighlight();
            UpdateToolbar();
        }

        private void RefreshRuntimeTargets()
        {
            if (toolbar == null)
                return;

            if (runtimeSession == null)
            {
                toolbar.SetTargets(null, -1, false);
                return;
            }

            IReadOnlyList<RuntimeSession.Target> targets = runtimeSession.Targets;
            var labels = new List<string>(targets.Count);

            for (int i = 0; i < targets.Count; i++)
                labels.Add(targets[i].Label);

            bool visible = !IsPreviewing
                && labels.Count > 0
                && (labels.Count > 1 || runtimeSession.SelectedIndex < 0);
            toolbar.SetTargets(labels, runtimeSession.SelectedIndex, visible);
        }

        private void UpdateToolbar()
        {
            if (toolbar == null)
                return;

            toolbar.SetLock(selectionLocked);
            toolbar.SetSpeed(preview != null ? preview.Speed : layout.previewSpeed);

            GraphRuntime runtime = CurrentRuntime;
            string statusText;
            Color statusColor;
            string statusTooltip = string.Empty;

            if (!Application.isPlaying && preview != null && !string.IsNullOrEmpty(preview.LastError))
            {
                statusText = "Preview error";
                statusColor = GraphEditorStyles.ErrorColor;
                statusTooltip = preview.LastError;
            }
            else if (IsPreviewing)
            {
                if (previewStale)
                {
                    statusText = "Preview (out of date)";
                    statusColor = GraphEditorStyles.WarningColor;
                    statusTooltip = "The graph changed. Click Restart to apply the changes.";
                }
                else if (preview.IsPaused)
                {
                    statusText = "Preview paused";
                    statusColor = GraphEditorStyles.PreviewColor;
                    statusTooltip = "Active: " + BuildActivePath(currentSnapshot);
                }
                else
                {
                    statusText = "Preview";
                    statusColor = GraphEditorStyles.PreviewColor;
                    statusTooltip = "Active: " + BuildActivePath(currentSnapshot);
                }
            }
            else if (Application.isPlaying)
            {
                if (runtime != null)
                {
                    statusText = "Live";
                    statusColor = GraphEditorStyles.LiveColor;
                    string targetLabel = runtimeSession?.SelectedTarget?.Label ?? "Runtime";
                    statusTooltip = targetLabel + "\nActive: " + BuildActivePath(currentSnapshot);
                }
                else if (runtimeSession != null && runtimeSession.Targets.Count > 0)
                {
                    statusText = "Select live runtime";
                    statusColor = GraphEditorStyles.WarningColor;
                    statusTooltip =
                        "Live runtimes exist for other graphs. Pick one from the target dropdown.";
                }
                else
                {
                    statusText = "No live runtime";
                    statusColor = GraphEditorStyles.WarningColor;
                    statusTooltip =
                        "Create a GraphRuntime in code; the editor discovers it automatically. "
                        + "No MonoBehaviour is required.";
                }
            }
            else
            {
                statusText = "Preview stopped";
                statusColor = GraphEditorStyles.MutedTextColor;
                statusTooltip = "Click Play to preview this graph without entering Play Mode.";
            }

            toolbar.SetRuntimeStatus(statusText, statusColor, statusTooltip);

            bool playEnabled;
            bool isRunning;
            bool isPaused;
            bool stepEnabled;
            bool restartEnabled;
            bool speedEnabled;

            if (Application.isPlaying)
            {
                playEnabled = runtime != null;
                isRunning = runtime != null;
                isPaused = runtime != null && runtime.IsPaused;
                stepEnabled = runtime != null;
                restartEnabled = runtimeSession != null && runtimeSession.CanRestart;
                speedEnabled = false;
            }
            else
            {
                playEnabled = preview != null && asset != null;
                isRunning = preview != null && preview.IsRunning;
                isPaused = preview != null && preview.IsPaused;
                stepEnabled = preview != null && preview.IsRunning;
                restartEnabled = preview != null && asset != null;
                speedEnabled = preview != null;
            }

            toolbar.SetRuntimeControls(
                playEnabled,
                isRunning,
                isPaused,
                stepEnabled,
                restartEnabled,
                speedEnabled);
        }

        public void OpenValidationTab()
        {
            OpenDrawer(BottomDrawer.Tab.Validation);
        }

        public void OpenRuntimeLogTab()
        {
            OpenDrawer(BottomDrawer.Tab.RuntimeLog);
        }

        private void OpenDrawer(BottomDrawer.Tab tab)
        {
            layout.drawerVisible = true;
            layout.drawerTab = (int)tab;
            ApplyLayoutVisibility();

            if (bottomDrawer != null)
                bottomDrawer.SetTab(tab, false);

            ClampLayout();
        }

        private void CloseDrawer()
        {
            layout.drawerVisible = false;
            ApplyLayoutVisibility();
            ClampLayout();
        }

        public void ToggleParametersPanel()
        {
            layout.parametersVisible = !layout.parametersVisible;
            ApplyLayoutVisibility();
            ClampLayout();
        }

        public void ToggleInspectorPanel()
        {
            layout.inspectorVisible = !layout.inspectorVisible;
            ApplyLayoutVisibility();
            ClampLayout();
        }

        public void ToggleGrid()
        {
            if (graphView != null)
                graphView.SetGridVisible(!graphView.IsGridVisible);
        }

        public void FrameAll()
        {
            graphView?.FrameAll();
        }

        public void FrameSelection()
        {
            graphView?.FrameSelectionSafe();
        }

        public void ResetZoom()
        {
            graphView?.ResetZoom();
        }

        public void OpenSearch()
        {
            if (controller == null)
                return;

            if (searchProvider == null)
                RebuildSearchProvider();

            if (searchProvider == null)
                return;

            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(rootVisualElement.worldBound.center);
            SearchWindow.Open(new SearchWindowContext(screenPoint), searchProvider);
        }

        private void RebuildSearchProvider()
        {
            DestroySearchProvider();

            searchProvider = ScriptableObject.CreateInstance<GraphSearchProvider>();
            searchProvider.hideFlags = HideFlags.HideAndDontSave;
            searchProvider.Initialize(controller, OnSearchResultSelected);
        }

        private void DestroySearchProvider()
        {
            if (searchProvider == null)
                return;

            UnityEngine.Object.DestroyImmediate(searchProvider);
            searchProvider = null;
        }

        private void OnSearchResultSelected(GraphSearchResult result)
        {
            if (result == null)
                return;

            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                NavigateTo(result.MachineId, result.StateId, result.TransitionId);
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsTextInput(evt.target))
                return;

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.F)
            {
                OpenSearch();
                evt.StopPropagation();
                return;
            }

            if (evt.altKey || evt.ctrlKey || evt.commandKey || evt.shiftKey)
                return;

            if (evt.keyCode == KeyCode.Space)
            {
                if (evt.target is Button || evt.target is Toggle)
                    return;

                TogglePreviewPlayback();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Period)
            {
                StepRuntime();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F)
            {
                graphView?.FrameSelectionSafe();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.A)
            {
                graphView?.FrameAll();
                evt.StopPropagation();
            }
        }

        private static bool IsTextInput(IEventHandler target)
        {
            var element = target as VisualElement;

            while (element != null)
            {
                if (element is TextField)
                    return true;

                element = element.parent;
            }

            return false;
        }

        private void ApplyLayoutVisibility()
        {
            if (blackboardPanel != null)
                blackboardPanel.style.display = layout.parametersVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            if (parametersSplitter != null)
                parametersSplitter.style.display = layout.parametersVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            if (inspectorPanel != null)
                inspectorPanel.style.display = layout.inspectorVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            if (inspectorSplitter != null)
                inspectorSplitter.style.display = layout.inspectorVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            if (bottomDrawer != null)
            {
                bottomDrawer.style.display = layout.drawerVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                bottomDrawer.SetTab((BottomDrawer.Tab)layout.drawerTab, false);
            }

            if (drawerSplitter != null)
                drawerSplitter.style.display = layout.drawerVisible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            ApplyPanelSizes();
        }

        private void ApplyPanelSizes()
        {
            parametersSplitter?.SetSizeWithoutNotify(layout.parametersWidth);
            inspectorSplitter?.SetSizeWithoutNotify(layout.inspectorWidth);
            drawerSplitter?.SetSizeWithoutNotify(layout.drawerHeight);
        }

        private void ClampLayout()
        {
            if (layoutClamping)
                return;

            layoutClamping = true;

            try
            {
                if (workspace != null && workspace.layout.width > 0f)
                {
                    float width = workspace.layout.width;
                    const float graphMin = 300f;

                    if (layout.parametersVisible)
                    {
                        float reservedForInspector = layout.inspectorVisible ? 240f : 0f;
                        float maxParameters = Mathf.Max(
                            180f,
                            width - graphMin - reservedForInspector);
                        layout.parametersWidth = Mathf.Clamp(
                            layout.parametersWidth,
                            180f,
                            maxParameters);
                    }

                    if (layout.inspectorVisible)
                    {
                        float reserved = layout.parametersVisible ? layout.parametersWidth : 0f;
                        float maxInspector = Mathf.Max(240f, width - graphMin - reserved);
                        layout.inspectorWidth = Mathf.Clamp(
                            layout.inspectorWidth,
                            240f,
                            maxInspector);
                    }
                }

                if (layout.drawerVisible && rootVisualElement.layout.height > 0f)
                {
                    float maxHeight = Mathf.Max(
                        120f,
                        rootVisualElement.layout.height - GraphEditorStyles.ToolbarHeight - 220f);
                    layout.drawerHeight = Mathf.Clamp(layout.drawerHeight, 120f, maxHeight);
                }

                ApplyPanelSizes();
            }
            finally
            {
                layoutClamping = false;
            }
        }

        private float MaxParametersWidth()
        {
            if (workspace == null || workspace.layout.width <= 0f)
                return float.PositiveInfinity;

            float reserved = layout.inspectorVisible ? Mathf.Max(240f, layout.inspectorWidth) : 0f;
            return Mathf.Max(180f, workspace.layout.width - reserved - 300f);
        }

        private float MaxInspectorWidth()
        {
            if (workspace == null || workspace.layout.width <= 0f)
                return float.PositiveInfinity;

            float reserved = layout.parametersVisible ? Mathf.Max(180f, layout.parametersWidth) : 0f;
            return Mathf.Max(240f, workspace.layout.width - reserved - 300f);
        }

        private float MaxDrawerHeight()
        {
            if (rootVisualElement.layout.height <= 0f)
                return float.PositiveInfinity;

            return Mathf.Max(
                120f,
                rootVisualElement.layout.height - GraphEditorStyles.ToolbarHeight - 220f);
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

            if (asset != null && (controller == null || preview == null))
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
            toolbar?.SetFollowStatus(text);
        }
    }
}
