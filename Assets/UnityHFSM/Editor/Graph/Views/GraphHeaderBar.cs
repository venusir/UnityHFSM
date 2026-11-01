using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Single command bar: document menu, navigation history, breadcrumb,
    /// mode-specific commands, Find and View.
    /// </summary>
    public sealed class GraphHeaderBar : GraphEditorView
    {
        private static readonly float[] SpeedValues = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly string[] SpeedChoices = { "0.1x", "0.25x", "0.5x", "1x", "2x", "4x" };

        private readonly Button documentButton;
        private readonly Button backButton;
        private readonly Button forwardButton;
        private readonly VisualElement breadcrumb;
        private readonly Button findButton;
        private readonly VisualElement authorGroup;
        private readonly Button addButton;
        private readonly Button linkButton;
        private readonly Button deleteButton;
        private readonly Button previewButton;
        private readonly VisualElement runtimeGroup;
        private readonly Button pauseButton;
        private readonly Button stepButton;
        private readonly VisualElement previewGroup;
        private readonly Button stopButton;
        private readonly Button previewMoreButton;
        private readonly VisualElement targetGroup;
        private readonly Button targetButton;
        private readonly Button viewButton;
        private string breadcrumbKey;

        public GraphHeaderBar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = GraphEditorMetrics.HeaderBarHeight;
            style.minHeight = GraphEditorMetrics.HeaderBarHeight;
            style.maxHeight = GraphEditorMetrics.HeaderBarHeight;
            style.flexShrink = 0f;
            style.paddingLeft = GraphEditorMetrics.Spacing;
            style.paddingRight = GraphEditorMetrics.Spacing;
            style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            style.borderBottomWidth = 1f;
            style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            style.overflow = Overflow.Hidden;

            documentButton = GraphEditorStyles.CreateToolButton(
                "No Graph",
                "Current graph asset. Click for document options.");
            documentButton.style.maxWidth = 200f;
            documentButton.style.minWidth = 90f;
            documentButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            documentButton.style.whiteSpace = WhiteSpace.NoWrap;
            documentButton.style.overflow = Overflow.Hidden;
            documentButton.style.textOverflow = TextOverflow.Ellipsis;
            documentButton.clicked += ShowDocumentMenu;
            Add(documentButton);

            Add(CreateSeparator());

            backButton = GraphEditorStyles.CreateToolButton("◀", "Back (Alt+Left)");
            backButton.clicked += () => Commands.Navigation.Back();
            Add(backButton);

            forwardButton = GraphEditorStyles.CreateToolButton("▶", "Forward (Alt+Right)");
            forwardButton.clicked += () => Commands.Navigation.Forward();
            Add(forwardButton);

            breadcrumb = new VisualElement();
            breadcrumb.style.flexDirection = FlexDirection.Row;
            breadcrumb.style.alignItems = Align.Center;
            breadcrumb.style.flexGrow = 1f;
            breadcrumb.style.flexShrink = 1f;
            breadcrumb.style.minWidth = 0f;
            breadcrumb.style.overflow = Overflow.Hidden;
            Add(breadcrumb);

            authorGroup = CreateGroup();
            Add(authorGroup);
            addButton = AddGroupButton(authorGroup, "+ Add", "Create a state or sub-state machine", ShowAddMenu);
            linkButton = AddGroupButton(authorGroup, "Link", "Create a transition by selecting source and target (L)", ToggleLink);
            deleteButton = AddGroupButton(authorGroup, "Delete", "Delete the selection (Delete)", DeleteSelection);
            previewButton = AddGroupButton(authorGroup, "Preview", "Start the edit-mode preview", StartPreview);

            runtimeGroup = CreateGroup();
            Add(runtimeGroup);
            pauseButton = AddGroupButton(runtimeGroup, "Pause", "Pause/resume the runtime (Space)", TogglePause);
            stepButton = AddGroupButton(runtimeGroup, "Step", "Pause and advance one logic tick (.)", Step);

            previewGroup = CreateGroup();
            Add(previewGroup);
            stopButton = AddGroupButton(previewGroup, "Stop", "Stop the preview and return to editing", StopPreview);
            previewMoreButton = AddGroupButton(previewGroup, "⋯", "Reset and preview speed", ShowPreviewMoreMenu);

            targetGroup = CreateGroup();
            Add(targetGroup);
            targetButton = AddGroupButton(targetGroup, "Target", "Select the live runtime to inspect", ShowTargetMenu);
            targetButton.style.maxWidth = 180f;
            targetButton.style.overflow = Overflow.Hidden;
            targetButton.style.textOverflow = TextOverflow.Ellipsis;

            findButton = GraphEditorStyles.CreateToolButton(
                "Find",
                "Search states, machines and transitions (Ctrl+F)");
            findButton.clicked += OnFindClicked;
            Add(findButton);

            viewButton = GraphEditorStyles.CreateToolButton("View", "View and layout options");
            viewButton.clicked += ShowViewMenu;
            Add(viewButton);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            GraphEditorMode mode = Session.Mode;
            StateMachineGraphAsset asset = Session.Asset;

            string documentText = asset != null ? asset.name : "No Graph";
            if (Session.State.selectionLocked)
                documentText = "Locked · " + documentText;

            documentButton.text = documentText;
            documentButton.tooltip = BuildDocumentTooltip(asset, mode);

            backButton.SetEnabled(Session.History.CanGoBack);
            forwardButton.SetEnabled(Session.History.CanGoForward);
            findButton.SetEnabled(Session.HasDocument);
            RebuildBreadcrumb();

            authorGroup.style.display = mode == GraphEditorMode.Author
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            runtimeGroup.style.display =
                mode == GraphEditorMode.Preview
                || mode == GraphEditorMode.Live
                || mode == GraphEditorMode.LiveNoTarget
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            previewGroup.style.display = mode == GraphEditorMode.Preview
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            targetGroup.style.display =
                mode == GraphEditorMode.Live || mode == GraphEditorMode.LiveNoTarget
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            bool canAuthor = GraphEditorModePolicy.AllowsAuthoring(mode) && Session.HasDocument;
            addButton.SetEnabled(canAuthor);
            linkButton.SetEnabled(canAuthor);
            linkButton.text = Session.IsLinking ? "Cancel Link" : "Link";
            GraphEditorSelectionKind selectionKind = Session.Selection.Kind;
            deleteButton.SetEnabled(canAuthor
                && (selectionKind == GraphEditorSelectionKind.State
                    || selectionKind == GraphEditorSelectionKind.Transition
                    || selectionKind == GraphEditorSelectionKind.Multi));
            previewButton.SetEnabled(GraphEditorModePolicy.CanStartPreview(mode) && Session.HasDocument);

            bool hasRuntime = Session.CurrentRuntime != null;
            pauseButton.text = Session.IsPaused ? "Resume" : "Pause";
            pauseButton.SetEnabled(GraphEditorModePolicy.CanPause(mode) && hasRuntime);
            stepButton.SetEnabled(GraphEditorModePolicy.CanStep(mode) && hasRuntime);
            stopButton.SetEnabled(GraphEditorModePolicy.CanStopPreview(mode));
            previewMoreButton.SetEnabled(mode == GraphEditorMode.Preview);

            targetButton.text = BuildTargetLabel();
            targetButton.tooltip = string.IsNullOrEmpty(Session.SelectedLiveTargetLabel)
                ? "No matching live runtime"
                : Session.SelectedLiveTargetLabel;
            targetButton.SetEnabled(GraphEditorModePolicy.CanSelectTarget(mode));
        }

        private string BuildDocumentTooltip(StateMachineGraphAsset asset, GraphEditorMode mode)
        {
            var builder = new StringBuilder();
            builder.Append(asset != null ? asset.name : "No graph asset selected.");
            builder.Append("\nMode: ").Append(FormatMode(mode));

            string follow = Session.State.selectionLocked
                ? "Locked"
                : Session.State.followedRunner != null
                    ? "Following " + Session.State.followedRunner.gameObject.name
                    : asset != null ? "Graph selected" : "No graph";
            builder.Append('\n').Append(follow);
            builder.Append("\nClick for document options.");
            return builder.ToString();
        }

        private static string FormatMode(GraphEditorMode mode)
        {
            switch (mode)
            {
                case GraphEditorMode.Author:
                    return "Edit";
                case GraphEditorMode.Preview:
                    return "Preview";
                case GraphEditorMode.Live:
                case GraphEditorMode.LiveNoTarget:
                    return "Live";
                default:
                    return "No Graph";
            }
        }

        private void ShowDocumentMenu()
        {
            if (Commands == null || Session == null)
                return;

            var menu = new GenericDropdownMenu();
            if (Session.Asset != null)
                menu.AddItem("Select in Project", false, SelectDocumentInProject);
            else
                menu.AddDisabledItem("Select in Project", false);

            menu.AddSeparator(string.Empty);

            bool canLock = GraphEditorModePolicy.AllowsDocumentFollow(Session.Mode);
            if (canLock)
                menu.AddItem("Lock Selection", Session.State.selectionLocked, ToggleSelectionLock);
            else
                menu.AddDisabledItem("Lock Selection", Session.State.selectionLocked);

            menu.DropDown(documentButton.worldBound, documentButton, DropdownMenuSizeMode.Content);
        }

        private void SelectDocumentInProject()
        {
            if (Session?.Asset == null)
                return;

            Selection.activeObject = Session.Asset;
            EditorGUIUtility.PingObject(Session.Asset);
        }

        private void ToggleSelectionLock()
        {
            Commands?.Document.SetLock(!Session.State.selectionLocked);
        }

        private void OnFindClicked()
        {
            if (Commands == null || Session == null || !Session.HasDocument)
                return;

            Vector2 point = GUIUtility.GUIToScreenPoint(findButton.worldBound.center);
            Commands.Search.Open(point);
        }

        private void ShowAddMenu()
        {
            if (Commands == null)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("State", false, () =>
            {
                Vector2 position = Commands.Navigation.Canvas != null
                    ? Commands.Navigation.Canvas.GetDefaultCreatePosition()
                    : Vector2.zero;
                Commands.Authoring.CreateState(position);
            });
            menu.AddItem("Sub-State Machine", false, () =>
            {
                Vector2 position = Commands.Navigation.Canvas != null
                    ? Commands.Navigation.Canvas.GetDefaultCreatePosition()
                    : Vector2.zero;
                Commands.Authoring.CreateSubMachine(position);
            });
            menu.DropDown(addButton.worldBound, addButton, DropdownMenuSizeMode.Content);
        }

        private void ToggleLink()
        {
            if (Commands == null)
                return;

            if (Session.IsLinking)
                Commands.Authoring.CancelLink();
            else
                Commands.Authoring.BeginLink();
        }

        private void DeleteSelection()
        {
            Commands?.Authoring.DeleteSelection();
        }

        private void StartPreview()
        {
            Commands?.Runtime.StartPreview();
        }

        private void TogglePause()
        {
            Commands?.Runtime.TogglePause();
        }

        private void Step()
        {
            Commands?.Runtime.Step();
        }

        private void StopPreview()
        {
            Commands?.Runtime.StopPreview();
        }

        private void ShowPreviewMoreMenu()
        {
            if (Commands == null)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Reset Preview", false, () => Commands.Runtime.ResetPreview());
            menu.AddSeparator(string.Empty);

            for (int i = 0; i < SpeedChoices.Length; i++)
            {
                int index = i;
                menu.AddItem(
                    SpeedChoices[i],
                    Mathf.Approximately(Session.PreviewSpeed, SpeedValues[i]),
                    () => Commands.Runtime.SetSpeed(SpeedValues[index]));
            }

            menu.DropDown(previewMoreButton.worldBound, previewMoreButton, DropdownMenuSizeMode.Content);
        }

        private void ShowTargetMenu()
        {
            if (Commands == null)
                return;

            var menu = new GenericDropdownMenu();
            if (Session.LiveTargets.Count == 0)
            {
                menu.AddDisabledItem("No matching live runtime", false);
            }
            else
            {
                for (int i = 0; i < Session.LiveTargets.Count; i++)
                {
                    int index = i;
                    menu.AddItem(
                        Session.LiveTargets[i].Label,
                        index == Session.SelectedLiveTargetIndex,
                        () => Commands.Runtime.SelectTarget(index));
                }
            }

            menu.DropDown(targetButton.worldBound, targetButton, DropdownMenuSizeMode.Content);
        }

        private void ShowViewMenu()
        {
            if (Commands == null)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Grid", Session.GridVisible, Commands.Layout.ToggleGrid);
            menu.AddItem("Frame All", false, Commands.Navigation.FrameAll);
            menu.AddItem("Center on Selection", false, Commands.Navigation.FrameSelection);
            menu.AddItem("Reset Zoom", false, Commands.Navigation.ResetZoom);
            menu.AddSeparator(string.Empty);
            menu.AddItem("Parameters Panel", Session.Layout.parametersVisible,
                Commands.Layout.ToggleParametersPanel);
            menu.AddItem("Inspector Panel", Session.Layout.inspectorVisible,
                Commands.Layout.ToggleInspectorPanel);
            menu.DropDown(viewButton.worldBound, viewButton, DropdownMenuSizeMode.Content);
        }

        private string BuildTargetLabel()
        {
            string label = Session.SelectedLiveTargetLabel;
            return string.IsNullOrEmpty(label) ? "No live runtime" : "Target: " + label;
        }

        private void RebuildBreadcrumb()
        {
            List<MachineDef> chain = Session.BuildMachineChain(Session.CurrentMachineId);
            var builder = new StringBuilder();
            for (int i = 0; i < chain.Count; i++)
                builder.Append(chain[i].id).Append(':').Append(chain[i].name).Append('|');

            string key = builder.ToString();
            if (key == breadcrumbKey)
                return;

            breadcrumbKey = key;
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
                Button button = GraphEditorStyles.CreateBreadcrumbButton(
                    string.IsNullOrEmpty(machine.name) ? "(unnamed)" : machine.name,
                    () => Commands.Navigation.NavigateTo(machineId, null, null, true));

                if (machine.id == Session.CurrentMachineId)
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                breadcrumb.Add(button);
            }
        }

        private static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.width = 1f;
            separator.style.height = 16f;
            separator.style.marginLeft = GraphEditorMetrics.SmallSpacing;
            separator.style.marginRight = GraphEditorMetrics.SmallSpacing;
            separator.style.backgroundColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            separator.style.flexShrink = 0f;
            return separator;
        }

        private static VisualElement CreateGroup()
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginLeft = GraphEditorMetrics.TinySpacing;
            group.style.marginRight = GraphEditorMetrics.TinySpacing;
            group.style.flexShrink = 0f;
            return group;
        }

        private static Button AddGroupButton(
            VisualElement group,
            string text,
            string tooltip,
            Action action)
        {
            Button button = GraphEditorStyles.CreateToolButton(text, tooltip, action);
            button.style.marginLeft = GraphEditorMetrics.TinySpacing;
            button.style.marginRight = GraphEditorMetrics.TinySpacing;
            group.Add(button);
            return button;
        }
    }
}
