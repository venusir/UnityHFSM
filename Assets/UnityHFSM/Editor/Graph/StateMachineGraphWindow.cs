using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Window shell: owns the serialized state, session, commands and views, and
    /// forwards Unity editor events into the session. Interaction lives in
    /// Commands; rendering lives in Views.
    /// </summary>
    public sealed class StateMachineGraphWindow : EditorWindow
    {
        [SerializeField] private GraphEditorWindowState state = new GraphEditorWindowState();

        private GraphEditorSession session;
        private GraphEditorCommands commands;
        private GraphDocumentBar documentBar;
        private GraphModeBar modeBar;
        private GraphLayoutView layoutView;
        private bool registryRefreshQueued;
        private bool initialFramePending;

        public GraphEditorSession Session => session;
        public GraphEditorCommands Commands => commands;
        public GraphEditorWindowState State => state;

        [MenuItem("Tools/UnityHFSM/State Machine Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<StateMachineGraphWindow>("UnityHFSM Graph");
            window.Show();
        }

        public static void OpenAsset(StateMachineGraphAsset graphAsset)
        {
            var window = GetWindow<StateMachineGraphWindow>("UnityHFSM Graph");
            window.Show();
            window.OpenGraph(graphAsset);
        }

        public void OpenGraph(StateMachineGraphAsset graphAsset)
        {
            commands?.Document.OpenAsset(graphAsset);
        }

        private void OnEnable()
        {
            if (session != null)
                DisposeEditor();

            EnsureState();

            minSize = new Vector2(840f, 480f);

            session = new GraphEditorSession(state);
            commands = new GraphEditorCommands(session);
            session.SetPlayMode(EditorApplication.isPlaying);
            initialFramePending = true;

            BuildUI();

            session.Initialize();

            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            GraphRuntimeRegistry.Changed += OnRuntimeRegistryChanged;

            if (!state.selectionLocked)
                session.FollowSelection(Selection.activeObject);
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            GraphRuntimeRegistry.Changed -= OnRuntimeRegistryChanged;

            DisposeEditor();
        }

        private void OnBeforeAssemblyReload()
        {
            DisposeEditor();
        }

        private void DisposeEditor()
        {
            if (rootVisualElement != null)
            {
                rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            }

            documentBar?.Unbind();
            modeBar?.Unbind();
            layoutView?.Unbind();
            documentBar = null;
            modeBar = null;
            layoutView = null;

            commands?.Dispose();
            commands = null;
            session?.Dispose();
            session = null;
        }

        private void EnsureState()
        {
            if (state == null)
                state = new GraphEditorWindowState();

            if (state.layout == null)
                state.layout = new GraphEditorLayoutState();

            if (state.console == null)
                state.console = new GraphEditorConsoleState();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.focusable = true;

            documentBar = new GraphDocumentBar();
            rootVisualElement.Add(documentBar);

            modeBar = new GraphModeBar();
            rootVisualElement.Add(modeBar);

            layoutView = new GraphLayoutView();
            layoutView.style.flexGrow = 1f;
            layoutView.style.minHeight = 0f;
            rootVisualElement.Add(layoutView);

            documentBar.Bind(session, commands);
            modeBar.Bind(session, commands);
            layoutView.Bind(session, commands);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            layoutView.Canvas.RegisterCallback<PointerDownEvent>(_ => rootVisualElement.Focus());
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (!initialFramePending || evt.newRect.width <= 1f || evt.newRect.height <= 1f)
                return;

            initialFramePending = false;
            layoutView?.Canvas?.FrameAll();
        }

        private void OnEditorUpdate()
        {
            if (session == null || session.IsDisposed)
                return;

            session.Tick(EditorApplication.timeSinceStartup);
            layoutView?.Canvas?.PollSelection();
        }

        private void OnSelectionChange()
        {
            if (session == null || state == null || state.selectionLocked)
                return;

            if (!GraphEditorModePolicy.AllowsDocumentFollow(session.Mode))
                return;

            session.FollowSelection(Selection.activeObject);
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (session == null)
                return;

            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    session.SetPlayMode(true);
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    session.SetPlayMode(true);
                    session.RefreshLiveTargets();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    session.SetPlayMode(false);
                    break;
            }
        }

        private void OnUndoRedo()
        {
            session?.OnUndoRedo();
        }

        private void OnRuntimeRegistryChanged()
        {
            if (registryRefreshQueued)
                return;

            registryRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                registryRefreshQueued = false;
                if (this == null || session == null || session.IsDisposed)
                    return;

                session.RefreshLiveTargets();
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (session == null || commands == null)
                return;

            if (IsValueInput(evt.target))
                return;

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.F)
            {
                OpenSearch();
                evt.StopPropagation();
                return;
            }

            if (evt.altKey && evt.keyCode == KeyCode.LeftArrow)
            {
                if (session.History.CanGoBack)
                    commands.Navigation.Back();

                evt.StopPropagation();
                return;
            }

            if (evt.altKey && evt.keyCode == KeyCode.RightArrow)
            {
                if (session.History.CanGoForward)
                    commands.Navigation.Forward();

                evt.StopPropagation();
                return;
            }

            if (evt.ctrlKey || evt.commandKey || evt.altKey)
                return;

            if (evt.keyCode == KeyCode.Escape)
            {
                HandleEscape();
                evt.StopPropagation();
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    if (IsInteractiveControl(evt.target))
                        break;

                    if (evt.shiftKey)
                    {
                        if (GraphEditorModePolicy.CanResetPreview(session.Mode))
                            commands.Runtime.ResetPreview();
                    }
                    else if (GraphEditorModePolicy.CanPause(session.Mode))
                    {
                        commands.Runtime.TogglePause();
                    }

                    evt.StopPropagation();
                    break;

                case KeyCode.Period:
                    if (GraphEditorModePolicy.CanStep(session.Mode))
                        commands.Runtime.Step();

                    evt.StopPropagation();
                    break;

                case KeyCode.Return:
                    if (session.Selection.Kind == GraphEditorSelectionKind.State)
                    {
                        StateDef selected = session.FindState(session.Selection.StateId);
                        if (selected != null && selected.kind == StateKind.HybridSubMachine)
                            commands.Navigation.OpenSubMachine(selected.id);
                    }

                    evt.StopPropagation();
                    break;

                case KeyCode.F:
                    commands.Navigation.FrameSelection();
                    evt.StopPropagation();
                    break;

                case KeyCode.A:
                    commands.Navigation.FrameAll();
                    evt.StopPropagation();
                    break;

                case KeyCode.Delete:
                    layoutView?.Canvas?.PollSelection();
                    GraphEditorSelectionKind deleteKind = session.Selection.Kind;
                    if (GraphEditorModePolicy.AllowsAuthoring(session.Mode)
                        && (deleteKind == GraphEditorSelectionKind.State
                            || deleteKind == GraphEditorSelectionKind.Transition
                            || deleteKind == GraphEditorSelectionKind.Multi))
                    {
                        commands.Authoring.DeleteSelection();
                    }

                    evt.StopPropagation();
                    break;
            }
        }

        private void HandleEscape()
        {
            if (session.IsLinking)
            {
                commands.Authoring.CancelLink();
                return;
            }

            if (session.Selection.HasSelection)
            {
                commands.Navigation.ClearSelection();
                return;
            }

            if (session.Console.visible)
                commands.Layout.CloseConsole();
        }

        private void OpenSearch()
        {
            if (commands == null || !session.HasDocument)
                return;

            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(rootVisualElement.worldBound.center);
            commands.Search.Open(screenPoint);
        }

        private static bool IsValueInput(IEventHandler target)
        {
            var element = target as VisualElement;
            while (element != null)
            {
                if (element is TextField || element.ClassListContains("unity-base-field"))
                    return true;

                element = element.parent;
            }

            return false;
        }

        private static bool IsInteractiveControl(IEventHandler target)
        {
            var element = target as VisualElement;
            while (element != null)
            {
                if (element is Button || element is Toggle || element is TextField
                    || element.ClassListContains("unity-base-field"))
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }
    }
}
