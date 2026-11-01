using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Single interaction state for the graph editor. Owns the document, mode,
    /// selection, navigation history, runtime services, validation and layout.
    /// Commands mutate it; views only read it and subscribe to Changed.
    /// </summary>
    public sealed class GraphEditorSession : IDisposable
    {
        private readonly GraphEditorWindowState state;
        private readonly GraphEditorNavigationHistory history = new GraphEditorNavigationHistory();

        private GraphEditorController controller;
        private GraphPreviewSession preview;
        private LiveRuntimeSession live;

        private GraphEditorMode mode = GraphEditorMode.NoDocument;
        private GraphEditorSelection selection = GraphEditorSelection.None;
        private string currentMachineId = string.Empty;
        private GraphRuntimeSnapshot snapshot;
        private GraphValidationResult validation;
        private string statusMessage = string.Empty;
        private bool statusIsError;
        private double statusUntil;
        private double currentTime;
        private double nextRuntimePoll;
        private double nextTargetRefresh;
        private double nextValidationRefresh;
        private bool validationDirty;
        private bool isPlayMode;
        private bool linkMode;
        private string lastReportedPreviewError = string.Empty;
        private GraphEditorChange pendingStructureChange;
        private bool structureRefreshQueued;
        private bool disposed;

        public GraphEditorSession(GraphEditorWindowState state)
        {
            this.state = state ?? new GraphEditorWindowState();
            this.state.layout = this.state.layout ?? new GraphEditorLayoutState();
            this.state.console = this.state.console ?? new GraphEditorConsoleState();

            if (this.state.console.tab != GraphConsoleTab.Validation
                && this.state.console.tab != GraphConsoleTab.RuntimeLog)
            {
                this.state.console.tab = GraphConsoleTab.Validation;
            }

            if (IsInvalidSize(this.state.layout.parametersWidth))
                this.state.layout.parametersWidth = 250f;
            if (IsInvalidSize(this.state.layout.inspectorWidth))
                this.state.layout.inspectorWidth = 320f;
            if (IsInvalidSize(this.state.layout.consoleHeight))
                this.state.layout.consoleHeight = 220f;
            if (IsInvalidSize(this.state.layout.previewSpeed))
                this.state.layout.previewSpeed = 1f;
            this.state.layout.previewSpeed = Mathf.Clamp(this.state.layout.previewSpeed, 0.1f, 4f);

            if (!string.IsNullOrEmpty(this.state.lastMachineId))
                currentMachineId = this.state.lastMachineId;
        }

        public event Action<GraphEditorChange> Changed;

        public GraphEditorWindowState State => state;
        public StateMachineGraphAsset Asset => state.asset;
        public bool HasDocument => controller != null && controller.Model != null;
        public GraphModel Model => controller != null ? controller.Model : null;
        internal GraphEditorController Controller => controller;

        public GraphEditorMode Mode => mode;
        public GraphEditorSelection Selection => selection;
        public string CurrentMachineId => currentMachineId;
        public GraphEditorNavigationHistory History => history;
        public GraphEditorLayoutState Layout => state.layout;
        public GraphEditorConsoleState Console => state.console;
        public bool IsPlayMode => isPlayMode;
        public bool IsPreviewing => preview != null && preview.IsRunning;
        public bool IsPreviewPaused => preview != null && preview.IsPaused;
        public bool IsPaused =>
            IsPreviewing
                ? preview.IsPaused
                : isPlayMode && LiveCurrentRuntime != null && LiveCurrentRuntime.IsPaused;
        public bool IsDisposed => disposed;
        public bool GridVisible => state.gridVisible;

        public GraphRuntime CurrentRuntime
        {
            get
            {
                if (IsPreviewing)
                    return preview.Runtime;

                return isPlayMode ? LiveCurrentRuntime : null;
            }
        }

        public GraphRuntimeSnapshot Snapshot => snapshot;
        public GraphValidationResult Validation => validation;
        public string PreviewLastError => preview != null ? preview.LastError : null;
        public string StatusMessage => statusMessage;
        public bool StatusIsError => statusIsError;
        public string PendingLinkSourceId { get; private set; } = string.Empty;
        public bool IsLinking => linkMode;
        public float PreviewSpeed => state.layout.previewSpeed;

        public IReadOnlyList<LiveRuntimeSession.Target> LiveTargets =>
            live != null ? live.Targets : (IReadOnlyList<LiveRuntimeSession.Target>)Array.Empty<LiveRuntimeSession.Target>();

        public int SelectedLiveTargetIndex => live != null ? live.SelectedIndex : -1;

        public string SelectedLiveTargetLabel
        {
            get
            {
                LiveRuntimeSession.Target target = live?.SelectedTarget;
                return target != null ? target.Label : string.Empty;
            }
        }

        public GameObject PreviewHost
        {
            get
            {
                if (preview != null && preview.Host != null)
                    return preview.Host;

                return ResolvePreviewHost();
            }
        }

        private GraphRuntime LiveCurrentRuntime => live != null ? live.CurrentRuntime : null;

        public void Initialize()
        {
            if (disposed)
                return;

            if (state.asset != null)
                OpenAsset(state.asset, state.followedRunner);
            else
            {
                controller = null;
                mode = GraphEditorMode.NoDocument;
                Notify(GraphEditorChange.All);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            DetachController();
            preview?.Dispose();
            preview = null;
            live?.Dispose();
            live = null;
            snapshot = null;
            selection = GraphEditorSelection.None;
            Changed = null;
        }

        public GraphCommandResult OpenAsset(
            StateMachineGraphAsset asset,
            StateMachineRunner runner = null)
        {
            if (disposed)
                return Blocked(GraphCommandBlockReason.Unknown, "The editor session is disposed.");

            if (asset == state.asset && controller != null)
            {
                state.followedRunner = runner;
                Notify(GraphEditorChange.Document | GraphEditorChange.Status);
                return GraphCommandResult.Ok();
            }

            StopPreviewInternal();
            CancelLinkInternal();
            lastReportedPreviewError = string.Empty;
            pendingStructureChange = GraphEditorChange.None;
            ClearStatus();
            DetachController();
            preview?.Dispose();
            preview = null;
            live?.Dispose();
            live = null;
            history.Reset();
            snapshot = null;

            state.asset = asset;
            state.followedRunner = runner;

            if (asset == null)
            {
                controller = null;
                currentMachineId = string.Empty;
                selection = GraphEditorSelection.None;
                validation = null;
                mode = GraphEditorMode.NoDocument;
                SaveNavigationState();
                Notify(GraphEditorChange.All);
                return GraphCommandResult.Ok();
            }

            controller = new GraphEditorController(asset);
            controller.InitializeIfEmpty();
            AttachController();

            currentMachineId = state.lastMachineId;
            if (Model.FindMachine(currentMachineId) == null)
                currentMachineId = Model.rootMachineId;

            preview = new GraphPreviewSession(
                asset.Graph,
                ResolvePreviewHost(),
                state.layout.previewSpeed);

            live = new LiveRuntimeSession();
            live.SetGraphId(asset.Graph.graphId);

            if (isPlayMode)
            {
                live.RefreshTargets();
                live.Poll();
                snapshot = live.Snapshot;
            }

            selection = RestoreSelection();
            history.Push(new GraphEditorNavigationTarget(currentMachineId, selection));
            validation = Model != null ? GraphValidator.Validate(Model) : null;
            RecomputeMode(false);
            SaveNavigationState();
            Notify(GraphEditorChange.All);
            return GraphCommandResult.Ok();
        }

        public void SetPlayMode(bool value)
        {
            if (disposed || isPlayMode == value)
                return;

            isPlayMode = value;
            lastReportedPreviewError = string.Empty;
            ClearStatus();

            if (value)
            {
                StopPreviewInternal();
                CancelLinkInternal();
                live?.RefreshTargets();
                live?.Poll();
                snapshot = live != null ? live.Snapshot : null;
                nextTargetRefresh = currentTime + 1d;
            }
            else
            {
                snapshot = null;
                live?.Dispose();
                if (state.asset != null)
                {
                    live = new LiveRuntimeSession();
                    live.SetGraphId(state.asset.Graph.graphId);
                }
            }

            RecomputeMode(false);
            Notify(GraphEditorChange.Mode
                | GraphEditorChange.Runtime
                | GraphEditorChange.Targets);
        }

        public void Tick(double timeSinceStartup)
        {
            if (disposed)
                return;

            currentTime = timeSinceStartup;

            if (statusUntil > 0d && currentTime >= statusUntil)
            {
                statusUntil = 0d;
                statusMessage = string.Empty;
                statusIsError = false;
                Notify(GraphEditorChange.Status);
            }

            if (validationDirty && currentTime >= nextValidationRefresh)
            {
                validationDirty = false;
                validateAndNotify(GraphEditorChange.Validation);
            }

            if (!isPlayMode && preview != null && preview.IsRunning)
            {
                preview.Advance(timeSinceStartup);

                string previewError = preview.LastError;
                if (!string.IsNullOrEmpty(previewError)
                    && !string.Equals(previewError, lastReportedPreviewError, StringComparison.Ordinal))
                {
                    lastReportedPreviewError = previewError;
                    ReportStatus(previewError, true, true);
                    OpenConsole(GraphConsoleTab.RuntimeLog);
                }
                else if (string.IsNullOrEmpty(previewError))
                {
                    lastReportedPreviewError = string.Empty;
                }

                if (currentTime >= nextRuntimePoll)
                {
                    nextRuntimePoll = currentTime + 0.1d;
                    snapshot = preview.GetSnapshot();
                    Notify(GraphEditorChange.Runtime);
                }
            }
            else if (isPlayMode && live != null)
            {
                if (currentTime >= nextTargetRefresh)
                {
                    nextTargetRefresh = currentTime + 1d;
                    live.RefreshTargets();
                    Notify(GraphEditorChange.Targets);
                    RecomputeMode(true);
                }

                if (currentTime >= nextRuntimePoll)
                {
                    nextRuntimePoll = currentTime + 0.1d;
                    live.Poll();
                    GraphRuntimeSnapshot next = live.Snapshot;

                    if (next != null || snapshot != null)
                    {
                        snapshot = next;
                        Notify(GraphEditorChange.Runtime);
                    }
                }
            }
        }

        public GraphCommandResult StartPreview()
        {
            if (!GraphEditorModePolicy.CanStartPreview(mode) || preview == null)
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Preview is not available in this mode.");

            if (!preview.Start())
            {
                if (preview.LastValidation != null)
                {
                    validation = preview.LastValidation;
                    Notify(GraphEditorChange.Validation);
                }

                OpenConsole(GraphConsoleTab.Validation);
                ReportStatus(preview.LastError, true, true);
                return Blocked(GraphCommandBlockReason.Unknown, preview.LastError);
            }

            snapshot = preview.GetSnapshot();
            nextRuntimePoll = currentTime;
            lastReportedPreviewError = string.Empty;
            CancelLinkInternal();
            RecomputeMode(true);
            Notify(GraphEditorChange.Runtime);
            ReportStatus("Preview started. The graph is read-only.", false);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult StopPreview()
        {
            if (preview == null || !preview.IsRunning)
                return Blocked(GraphCommandBlockReason.Unknown, "Preview is not running.");

            StopPreviewInternal();
            RecomputeMode(true);
            Notify(GraphEditorChange.Runtime);
            ReportStatus("Preview stopped.", false);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult ResetPreview()
        {
            if (!GraphEditorModePolicy.CanResetPreview(mode) || preview == null)
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Reset is only available while previewing.");

            if (!preview.Restart(true))
            {
                if (preview.LastValidation != null)
                {
                    validation = preview.LastValidation;
                    Notify(GraphEditorChange.Validation);
                }

                OpenConsole(GraphConsoleTab.Validation);
                ReportStatus(preview.LastError, true, true);
                return Blocked(GraphCommandBlockReason.Unknown, preview.LastError);
            }

            snapshot = preview.GetSnapshot();
            nextRuntimePoll = currentTime;
            lastReportedPreviewError = string.Empty;
            Notify(GraphEditorChange.Runtime);
            ReportStatus("Preview reset.", false);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult TogglePause()
        {
            if (IsPreviewing)
            {
                preview.TogglePause();
                Notify(GraphEditorChange.Runtime);
                return GraphCommandResult.Ok();
            }

            GraphRuntime runtime = LiveCurrentRuntime;
            if (runtime == null)
                return Blocked(GraphCommandBlockReason.NoRuntime, "No live runtime is selected.");

            if (runtime.IsPaused)
                runtime.Resume();
            else
                runtime.Pause();

            Notify(GraphEditorChange.Runtime);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult Step()
        {
            if (IsPreviewing)
            {
                if (!preview.IsPaused)
                    preview.Pause();

                preview.Step();
                snapshot = preview.GetSnapshot();

                if (!string.IsNullOrEmpty(preview.LastError)
                    && !string.Equals(preview.LastError, lastReportedPreviewError, StringComparison.Ordinal))
                {
                    lastReportedPreviewError = preview.LastError;
                    ReportStatus(preview.LastError, true, true);
                    OpenConsole(GraphConsoleTab.RuntimeLog);
                }

                Notify(GraphEditorChange.Runtime);
                return GraphCommandResult.Ok();
            }

            GraphRuntime runtime = LiveCurrentRuntime;
            if (runtime == null)
                return Blocked(GraphCommandBlockReason.NoRuntime, "No live runtime is selected.");

            if (!runtime.IsPaused)
                runtime.Pause();

            runtime.Step(1f / 60f);
            live.Poll();
            snapshot = live.Snapshot;
            Notify(GraphEditorChange.Runtime);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetPreviewSpeed(float speed)
        {
            if (mode == GraphEditorMode.Live || mode == GraphEditorMode.LiveNoTarget)
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Preview speed is not used in Play Mode.");

            state.layout.previewSpeed = Mathf.Clamp(speed, 0.1f, 4f);

            if (preview != null)
                preview.Speed = state.layout.previewSpeed;

            Notify(GraphEditorChange.Layout);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetPreviewHost(GameObject host)
        {
            if (mode == GraphEditorMode.Live
                || mode == GraphEditorMode.LiveNoTarget
                || IsPreviewing)
            {
                return Blocked(
                    GraphCommandBlockReason.ReadOnlyMode,
                    "Stop the preview before changing its host.");
            }

            state.previewHostGlobalId = host != null
                ? GlobalObjectId.GetGlobalObjectIdSlow(host).ToString()
                : string.Empty;

            if (preview != null)
                preview.Host = host;

            Notify(GraphEditorChange.Runtime | GraphEditorChange.Layout);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SelectLiveTarget(int index)
        {
            if (!GraphEditorModePolicy.CanSelectTarget(mode) || live == null)
                return Blocked(GraphCommandBlockReason.InvalidTarget, "Target selection is only available in Play Mode.");

            if (index < 0 || index >= live.Targets.Count)
                return Blocked(GraphCommandBlockReason.InvalidTarget, "Unknown live runtime target.");

            live.SelectTarget(index);
            live.Poll();
            snapshot = live.Snapshot;
            RecomputeMode(true);
            Notify(GraphEditorChange.Targets | GraphEditorChange.Runtime);
            return GraphCommandResult.Ok();
        }

        public void RefreshLiveTargets()
        {
            if (disposed || live == null)
                return;

            live.RefreshTargets();
            if (isPlayMode)
            {
                live.Poll();
                snapshot = live.Snapshot;
            }

            RecomputeMode(true);
            Notify(GraphEditorChange.Targets | GraphEditorChange.Runtime);
        }

        public GraphCommandResult FollowSelection(UnityEngine.Object selected)
        {
            if (state.selectionLocked)
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Selection following is locked.");

            if (!GraphEditorModePolicy.AllowsDocumentFollow(mode))
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "The document is pinned while a runtime is active.");

            if (selected is StateMachineGraphAsset asset)
                return OpenAsset(asset);

            if (selected is GameObject gameObject)
            {
                StateMachineRunner runner = gameObject.GetComponent<StateMachineRunner>();
                if (runner != null && runner.Graph != null)
                    return OpenAsset(runner.Graph, runner);
            }

            return Blocked(GraphCommandBlockReason.InvalidTarget, "Selection has no graph.");
        }

        public void SetSelectionLocked(bool value)
        {
            if (state.selectionLocked == value)
                return;

            state.selectionLocked = value;
            Notify(GraphEditorChange.Lock);
        }

        public void SetSelection(GraphEditorSelection value, bool fromCanvas = false)
        {
            GraphEditorSelection next = value ?? GraphEditorSelection.None;
            if (selection.Equals(next))
                return;

            selection = next;
            SaveNavigationState();
            Notify(GraphEditorChange.Selection);
        }

        public void ClearSelection()
        {
            SetSelection(GraphEditorSelection.Machine(currentMachineId));
        }

        public GraphCommandResult NavigateTo(
            string machineId,
            string stateId,
            string transitionId,
            bool pushHistory)
        {
            if (controller == null || Model == null)
                return Blocked(GraphCommandBlockReason.NoDocument, "No graph is loaded.");

            if (string.IsNullOrEmpty(machineId))
            {
                machineId = GraphNavigation.ResolveMachineIdForState(Model, stateId);
                if (string.IsNullOrEmpty(machineId))
                    machineId = GraphNavigation.ResolveMachineIdForTransition(Model, transitionId);
            }

            if (string.IsNullOrEmpty(machineId) || Model.FindMachine(machineId) == null)
                return Blocked(GraphCommandBlockReason.InvalidTarget, "The target machine could not be found.");

            bool machineChanged = !string.Equals(machineId, currentMachineId, StringComparison.Ordinal);
            currentMachineId = machineId;
            state.lastMachineId = machineId;
            CancelLinkInternal();

            GraphEditorSelection next;
            if (!string.IsNullOrEmpty(transitionId) && Model.FindTransition(transitionId) != null)
                next = GraphEditorSelection.Transition(transitionId);
            else if (!string.IsNullOrEmpty(stateId) && Model.FindState(stateId) != null)
                next = GraphEditorSelection.State(stateId);
            else
                next = GraphEditorSelection.Machine(machineId);

            selection = next;
            SaveNavigationState();

            if (pushHistory)
                history.Push(new GraphEditorNavigationTarget(machineId, next));

            Notify((machineChanged ? GraphEditorChange.Machine : GraphEditorChange.None)
                | GraphEditorChange.Selection);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult Back()
        {
            if (!history.TryBack(out GraphEditorNavigationTarget target))
                return Blocked(GraphCommandBlockReason.InvalidTarget, "No previous location.");

            ApplyNavigationTarget(target);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult Forward()
        {
            if (!history.TryForward(out GraphEditorNavigationTarget target))
                return Blocked(GraphCommandBlockReason.InvalidTarget, "No next location.");

            ApplyNavigationTarget(target);
            return GraphCommandResult.Ok();
        }

        public void SetCurrentMachine(string machineId)
        {
            if (controller == null || Model == null || Model.FindMachine(machineId) == null)
                return;

            currentMachineId = machineId;
            state.lastMachineId = machineId;
            CancelLinkInternal();
            selection = GraphEditorSelection.Machine(machineId);
            SaveNavigationState();
            Notify(GraphEditorChange.Machine | GraphEditorChange.Selection);
        }

        public GraphCommandResult OpenSubMachine(string hostStateId)
        {
            StateDef host = FindState(hostStateId);
            if (host == null || string.IsNullOrEmpty(host.subMachineId))
                return Blocked(GraphCommandBlockReason.InvalidTarget, "The selected state has no sub-state machine.");

            return NavigateTo(host.subMachineId, null, null, true);
        }

        public GraphCommandResult BeginLink()
        {
            if (!GraphEditorModePolicy.AllowsAuthoring(mode))
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Link is only available while editing.");

            linkMode = true;
            PendingLinkSourceId = string.Empty;
            Notify(GraphEditorChange.Interaction);
            ReportStatus("Select the source state for the transition.", false);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetLinkSource(string stateId)
        {
            if (!GraphEditorModePolicy.AllowsAuthoring(mode))
                return Blocked(GraphCommandBlockReason.ReadOnlyMode, "Link is only available while editing.");

            if (FindState(stateId) == null)
                return Blocked(GraphCommandBlockReason.InvalidTarget, "Unknown source state.");

            linkMode = true;
            PendingLinkSourceId = stateId;
            Notify(GraphEditorChange.Interaction);
            ReportStatus("Select the target state, or press Esc to cancel.", false);
            return GraphCommandResult.Ok();
        }

        public void CancelLink()
        {
            if (!linkMode && string.IsNullOrEmpty(PendingLinkSourceId))
                return;

            linkMode = false;
            PendingLinkSourceId = string.Empty;
            Notify(GraphEditorChange.Interaction);
            ReportStatus("Link cancelled.", false);
        }

        public void ClearLinkSource()
        {
            if (!linkMode && string.IsNullOrEmpty(PendingLinkSourceId))
                return;

            linkMode = false;
            PendingLinkSourceId = string.Empty;
            Notify(GraphEditorChange.Interaction);
        }

        public void OpenConsole(GraphConsoleTab tab)
        {
            state.console.visible = true;
            state.console.tab = tab;
            Notify(GraphEditorChange.Console | GraphEditorChange.Layout);
        }

        public void ToggleConsole()
        {
            state.console.visible = !state.console.visible;
            Notify(GraphEditorChange.Console | GraphEditorChange.Layout);
        }

        public void CloseConsole()
        {
            if (!state.console.visible)
                return;

            state.console.visible = false;
            Notify(GraphEditorChange.Console | GraphEditorChange.Layout);
        }

        public void SetConsoleTab(GraphConsoleTab tab)
        {
            if (state.console.tab == tab && state.console.visible)
                return;

            state.console.tab = tab;
            state.console.visible = true;
            Notify(GraphEditorChange.Console | GraphEditorChange.Layout);
        }

        public void ToggleParametersPanel()
        {
            state.layout.parametersVisible = !state.layout.parametersVisible;
            Notify(GraphEditorChange.Layout);
        }

        public void ToggleInspectorPanel()
        {
            state.layout.inspectorVisible = !state.layout.inspectorVisible;
            Notify(GraphEditorChange.Layout);
        }

        public void SetParametersWidth(float width)
        {
            state.layout.parametersWidth = width;
            Notify(GraphEditorChange.Layout);
        }

        public void SetInspectorWidth(float width)
        {
            state.layout.inspectorWidth = width;
            Notify(GraphEditorChange.Layout);
        }

        public void SetConsoleHeight(float height)
        {
            state.layout.consoleHeight = height;
            Notify(GraphEditorChange.Layout);
        }

        public void ToggleGrid()
        {
            state.gridVisible = !state.gridVisible;
            Notify(GraphEditorChange.Layout);
        }

        public void ReportStatus(string message, bool isError, bool persistent = false)
        {
            statusMessage = message ?? string.Empty;
            statusIsError = isError;
            statusUntil = persistent || string.IsNullOrEmpty(statusMessage)
                ? 0d
                : currentTime + 4d;
            Notify(GraphEditorChange.Status);
        }

        public void ClearStatus()
        {
            if (string.IsNullOrEmpty(statusMessage) && !statusIsError)
                return;

            statusMessage = string.Empty;
            statusIsError = false;
            statusUntil = 0d;
            Notify(GraphEditorChange.Status);
        }

        public void Revalidate()
        {
            if (disposed)
                return;

            validationDirty = false;
            validateAndNotify(GraphEditorChange.Validation);
        }

        public void OnUndoRedo()
        {
            if (disposed)
                return;

            bool wasPreviewing = IsPreviewing;
            if (wasPreviewing)
            {
                StopPreviewInternal();
                lastReportedPreviewError = string.Empty;
                ClearStatus();
            }

            bool selectionChanged = PruneSelection();
            GraphValidationResult next = Model != null ? GraphValidator.Validate(Model) : null;
            validation = next;
            pendingStructureChange = GraphEditorChange.None;
            RecomputeMode(false);
            Notify(GraphEditorChange.StructureRebuild
                | GraphEditorChange.Validation
                | (selectionChanged ? GraphEditorChange.Selection : GraphEditorChange.None)
                | (wasPreviewing ? GraphEditorChange.Mode | GraphEditorChange.Runtime : GraphEditorChange.None));
        }

        public StateDef FindState(string stateId) =>
            Model != null ? Model.FindState(stateId) : null;

        public MachineDef FindMachine(string machineId) =>
            Model != null ? Model.FindMachine(machineId) : null;

        public TransitionDef FindTransition(string transitionId) =>
            Model != null ? Model.FindTransition(transitionId) : null;

        public ParameterDef FindParameter(string parameterId) =>
            Model != null ? Model.FindParameter(parameterId) : null;

        public List<MachineDef> BuildMachineChain(string machineId) =>
            GraphNavigation.BuildMachineChain(Model, string.IsNullOrEmpty(machineId) ? currentMachineId : machineId);

        public string BuildMachinePath(string machineId) =>
            GraphNavigation.BuildMachinePath(Model, string.IsNullOrEmpty(machineId) ? currentMachineId : machineId);

        public string BuildActivePath(GraphRuntimeSnapshot value)
        {
            GraphRuntimeSnapshot snapshotValue = value ?? snapshot;
            if (snapshotValue == null || snapshotValue.activePathStateIds.Count == 0)
                return "(none)";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshotValue.activePathStateIds.Count; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                StateDef state = FindState(snapshotValue.activePathStateIds[i]);
                builder.Append(state != null ? state.name : snapshotValue.activePathStateIds[i]);
            }

            return builder.ToString();
        }

        private void ApplyNavigationTarget(GraphEditorNavigationTarget target)
        {
            if (Model == null || Model.FindMachine(target.MachineId) == null)
                return;

            currentMachineId = target.MachineId;
            state.lastMachineId = target.MachineId;
            CancelLinkInternal();
            selection = target.Selection ?? GraphEditorSelection.Machine(target.MachineId);
            SaveNavigationState();
            Notify(GraphEditorChange.Machine | GraphEditorChange.Selection);
        }

        private void AttachController()
        {
            if (controller == null)
                return;

            controller.Changed += OnControllerChanged;
            controller.Dirty += OnControllerDirty;
        }

        private void DetachController()
        {
            if (controller == null)
                return;

            controller.Changed -= OnControllerChanged;
            controller.Dirty -= OnControllerDirty;
            controller = null;
        }

        private void OnControllerChanged()
        {
            bool machineChanged = false;
            if (Model != null && Model.FindMachine(currentMachineId) == null)
            {
                currentMachineId = Model.rootMachineId;
                state.lastMachineId = currentMachineId;
                selection = GraphEditorSelection.Machine(currentMachineId);
                machineChanged = true;
            }

            bool selectionChanged = PruneSelection() || machineChanged;
            GraphValidationResult next = Model != null ? GraphValidator.Validate(Model) : null;
            validation = next;

            // Notify selection/validation immediately; defer the structural rebuild
            // so a field callback (rename/condition edit) can finish before its
            // owning view is replaced.
            Notify(GraphEditorChange.Validation
                | (machineChanged ? GraphEditorChange.Machine : GraphEditorChange.None)
                | (selectionChanged ? GraphEditorChange.Selection : GraphEditorChange.None));

            pendingStructureChange |= GraphEditorChange.StructureRebuild;

            if (!structureRefreshQueued)
            {
                structureRefreshQueued = true;
                EditorApplication.delayCall += FlushStructureRefresh;
            }
        }

        private void FlushStructureRefresh()
        {
            structureRefreshQueued = false;
            if (disposed)
                return;

            GraphEditorChange change = pendingStructureChange;
            pendingStructureChange = GraphEditorChange.None;

            if (change != GraphEditorChange.None)
                Notify(change);
        }

        private void OnControllerDirty()
        {
            validationDirty = true;
            nextValidationRefresh = currentTime + 0.2d;
            Notify(GraphEditorChange.ModelDirty);
        }

        private void CancelLinkInternal()
        {
            if (!linkMode && string.IsNullOrEmpty(PendingLinkSourceId))
                return;

            linkMode = false;
            PendingLinkSourceId = string.Empty;
            Notify(GraphEditorChange.Interaction);
        }

        private void StopPreviewInternal()
        {
            if (preview == null || !preview.IsRunning)
                return;

            preview.Stop();
            snapshot = null;
            lastReportedPreviewError = string.Empty;
        }

        private void RecomputeMode(bool notify)
        {
            GraphEditorMode next = GraphEditorModePolicy.Resolve(
                state.asset != null,
                isPlayMode,
                IsPreviewing,
                LiveCurrentRuntime != null);

            if (next == mode)
                return;

            mode = next;
            if (notify)
                Notify(GraphEditorChange.Mode);
        }

        private void validateAndNotify(GraphEditorChange extra)
        {
            validation = Model != null ? GraphValidator.Validate(Model) : null;
            Notify(GraphEditorChange.Validation | extra);
        }

        private bool PruneSelection()
        {
            if (selection.IsNone || selection.Kind == GraphEditorSelectionKind.Machine)
                return false;

            var kept = new List<GraphEditorSelectionItem>();
            for (int i = 0; i < selection.Items.Count; i++)
            {
                GraphEditorSelectionItem item = selection.Items[i];
                switch (item.Kind)
                {
                    case GraphEditorSelectionItemKind.State:
                        if (Model != null && Model.FindState(item.Id) != null)
                            kept.Add(item);
                        break;
                    case GraphEditorSelectionItemKind.Transition:
                        if (Model != null && Model.FindTransition(item.Id) != null)
                            kept.Add(item);
                        break;
                    case GraphEditorSelectionItemKind.Pseudo:
                        kept.Add(item);
                        break;
                }
            }

            GraphEditorSelection next = kept.Count == 0
                ? GraphEditorSelection.Machine(currentMachineId)
                : GraphEditorSelection.Multi(kept);

            if (selection.Equals(next))
                return false;

            selection = next;
            SaveNavigationState();
            return true;
        }

        private GraphEditorSelection RestoreSelection()
        {
            if (!string.IsNullOrEmpty(state.selectedStateId)
                && Model != null
                && Model.FindState(state.selectedStateId) != null)
            {
                return GraphEditorSelection.State(state.selectedStateId);
            }

            if (!string.IsNullOrEmpty(state.selectedTransitionId)
                && Model != null
                && Model.FindTransition(state.selectedTransitionId) != null)
            {
                return GraphEditorSelection.Transition(state.selectedTransitionId);
            }

            return GraphEditorSelection.Machine(currentMachineId);
        }

        private void SaveNavigationState()
        {
            state.lastMachineId = currentMachineId ?? string.Empty;
            state.selectedStateId = selection != null && selection.Kind == GraphEditorSelectionKind.State
                ? selection.StateId
                : string.Empty;
            state.selectedTransitionId = selection != null && selection.Kind == GraphEditorSelectionKind.Transition
                ? selection.TransitionId
                : string.Empty;
        }

        private GameObject ResolvePreviewHost()
        {
            if (string.IsNullOrEmpty(state.previewHostGlobalId)
                || !GlobalObjectId.TryParse(state.previewHostGlobalId, out GlobalObjectId id))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        private void Notify(GraphEditorChange change)
        {
            if (disposed || change == GraphEditorChange.None)
                return;

            Changed?.Invoke(change);
        }

        private static GraphCommandResult Blocked(GraphCommandBlockReason reason, string message) =>
            GraphCommandResult.Blocked(reason, message);

        private static bool IsInvalidSize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f;
    }
}
