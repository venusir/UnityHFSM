using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Row 2: current mode pill, mode commands, status message, console/view.</summary>
    public sealed class GraphModeBar : GraphEditorView
    {
        private static readonly float[] SpeedValues = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly string[] SpeedChoices = { "0.1x", "0.25x", "0.5x", "1x", "2x", "4x" };

        private readonly Label modePill;
        private readonly VisualElement authorGroup;
        private readonly VisualElement previewGroup;
        private readonly VisualElement liveGroup;
        private readonly VisualElement liveNoTargetGroup;
        private readonly Label statusLabel;
        private readonly Button addButton;
        private readonly Button linkButton;
        private readonly Button deleteButton;
        private readonly Button previewButton;
        private readonly Button pauseButton;
        private readonly Button stepButton;
        private readonly Button resetButton;
        private readonly Button stopButton;
        private readonly Button speedButton;
        private readonly Button livePauseButton;
        private readonly Button liveStepButton;
        private readonly Button targetButton;
        private readonly Button noTargetButton;
        private readonly Button consoleButton;
        private readonly Button viewButton;
        private int lastErrorCount;

        public GraphModeBar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = GraphEditorMetrics.ModeBarHeight;
            style.minHeight = GraphEditorMetrics.ModeBarHeight;
            style.maxHeight = GraphEditorMetrics.ModeBarHeight;
            style.flexShrink = 0f;
            style.paddingLeft = GraphEditorMetrics.Spacing;
            style.paddingRight = GraphEditorMetrics.Spacing;
            style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            style.borderBottomWidth = 1f;
            style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);

            modePill = new Label("No Graph");
            modePill.style.height = 18f;
            modePill.style.paddingLeft = 8f;
            modePill.style.paddingRight = 8f;
            modePill.style.marginRight = GraphEditorMetrics.Spacing;
            modePill.style.fontSize = GraphEditorMetrics.SmallFontSize;
            modePill.style.unityFontStyleAndWeight = FontStyle.Bold;
            modePill.style.borderTopLeftRadius = 9f;
            modePill.style.borderTopRightRadius = 9f;
            modePill.style.borderBottomLeftRadius = 9f;
            modePill.style.borderBottomRightRadius = 9f;
            modePill.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(modePill);

            authorGroup = CreateGroup();
            Add(authorGroup);
            addButton = AddGroupButton(authorGroup, "+ Add", "Create a state or sub-state machine", ShowAddMenu);
            linkButton = AddGroupButton(authorGroup, "Link", "Create a transition by selecting source and target (L)", ToggleLink);
            deleteButton = AddGroupButton(authorGroup, "Delete", "Delete the selection (Delete)", DeleteSelection);
            previewButton = AddGroupButton(authorGroup, "Preview", "Start the edit-mode preview", StartPreview);

            previewGroup = CreateGroup();
            Add(previewGroup);
            pauseButton = AddGroupButton(previewGroup, "Pause", "Pause/resume the preview (Space)", TogglePause);
            stepButton = AddGroupButton(previewGroup, "Step", "Pause and advance one logic tick (.)", Step);
            resetButton = AddGroupButton(previewGroup, "Reset", "Rebuild the preview and keep parameter values (Shift+Space)", ResetPreview);
            stopButton = AddGroupButton(previewGroup, "Stop", "Stop the preview and return to editing", StopPreview);
            speedButton = AddGroupButton(previewGroup, "1x", "Preview speed", ShowSpeedMenu);

            liveGroup = CreateGroup();
            Add(liveGroup);
            livePauseButton = AddGroupButton(liveGroup, "Pause", "Pause/resume the live runtime (Space)", TogglePause);
            liveStepButton = AddGroupButton(liveGroup, "Step", "Pause and advance one logic tick (.)", Step);
            targetButton = AddGroupButton(liveGroup, "Target", "Select the live runtime to inspect", ShowTargetMenu);
            targetButton.style.maxWidth = 220f;
            targetButton.style.overflow = Overflow.Hidden;
            targetButton.style.textOverflow = TextOverflow.Ellipsis;

            liveNoTargetGroup = CreateGroup();
            Add(liveNoTargetGroup);
            noTargetButton = AddGroupButton(liveNoTargetGroup, "No live runtime", "No matching GraphRuntime was found", ShowTargetMenu);
            noTargetButton.style.maxWidth = 180f;

            statusLabel = new Label();
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.flexShrink = 1f;
            statusLabel.style.minWidth = 0f;
            statusLabel.style.marginLeft = GraphEditorMetrics.Spacing;
            statusLabel.style.marginRight = GraphEditorMetrics.Spacing;
            statusLabel.style.fontSize = GraphEditorMetrics.FontSize;
            statusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            statusLabel.style.overflow = Overflow.Hidden;
            statusLabel.style.textOverflow = TextOverflow.Ellipsis;
            Add(statusLabel);

            consoleButton = GraphEditorStyles.CreateToolButton("Console", "Open validation and runtime log");
            consoleButton.clicked += ToggleConsole;
            Add(consoleButton);

            viewButton = GraphEditorStyles.CreateToolButton("View", "View and layout options");
            viewButton.clicked += ShowViewMenu;
            Add(viewButton);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            GraphEditorMode mode = Session.Mode;
            SetModePill(mode);

            authorGroup.style.display = mode == GraphEditorMode.Author
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            previewGroup.style.display = mode == GraphEditorMode.Preview
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            liveGroup.style.display = mode == GraphEditorMode.Live
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            liveNoTargetGroup.style.display = mode == GraphEditorMode.LiveNoTarget
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

            pauseButton.text = Session.IsPreviewPaused ? "Resume" : "Pause";
            pauseButton.SetEnabled(GraphEditorModePolicy.CanPause(mode));
            stepButton.SetEnabled(GraphEditorModePolicy.CanStep(mode));
            resetButton.SetEnabled(GraphEditorModePolicy.CanResetPreview(mode));
            stopButton.SetEnabled(GraphEditorModePolicy.CanStopPreview(mode));
            speedButton.text = FormatSpeed(Session.PreviewSpeed);
            speedButton.SetEnabled(mode == GraphEditorMode.Preview);

            livePauseButton.text = Session.IsPaused ? "Resume" : "Pause";
            livePauseButton.SetEnabled(GraphEditorModePolicy.CanPause(mode) && Session.CurrentRuntime != null);
            liveStepButton.SetEnabled(GraphEditorModePolicy.CanStep(mode) && Session.CurrentRuntime != null);
            targetButton.text = BuildTargetLabel();
            targetButton.SetEnabled(mode == GraphEditorMode.Live && Session.LiveTargets.Count > 0);
            noTargetButton.text = "No live runtime";
            noTargetButton.SetEnabled(mode == GraphEditorMode.LiveNoTarget);

            string status = Session.StatusMessage;
            if (string.IsNullOrEmpty(status) && mode == GraphEditorMode.NoDocument)
                status = "Select a graph asset or a GameObject with a StateMachineRunner.";

            statusLabel.text = status;
            statusLabel.tooltip = status;
            statusLabel.style.color = new StyleColor(
                string.IsNullOrEmpty(status)
                    ? GraphEditorStyles.MutedTextColor
                    : Session.StatusIsError
                        ? GraphEditorStyles.ErrorColor
                        : GraphEditorStyles.MutedTextColor);

            UpdateConsoleButton();
        }

        private void SetModePill(GraphEditorMode mode)
        {
            Color color;
            string text;

            switch (mode)
            {
                case GraphEditorMode.Author:
                    color = GraphEditorStyles.MutedTextColor;
                    text = "Edit";
                    break;
                case GraphEditorMode.Preview:
                    color = GraphEditorStyles.PreviewColor;
                    text = "Preview";
                    break;
                case GraphEditorMode.Live:
                    color = GraphEditorStyles.LiveColor;
                    text = "Live";
                    break;
                case GraphEditorMode.LiveNoTarget:
                    color = GraphEditorStyles.LiveColor;
                    text = "Live";
                    break;
                default:
                    color = GraphEditorStyles.MutedTextColor;
                    text = "No Graph";
                    break;
            }

            modePill.text = text;
            modePill.style.color = new StyleColor(color);
            modePill.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.12f));
            modePill.tooltip = mode.ToString();
        }

        private void UpdateConsoleButton()
        {
            int errors = 0;
            int warnings = 0;

            if (Session.Validation != null)
            {
                for (int i = 0; i < Session.Validation.Issues.Count; i++)
                {
                    if (Session.Validation.Issues[i].severity == GraphIssueSeverity.Error)
                        errors++;
                    else
                        warnings++;
                }
            }

            lastErrorCount = errors;
            consoleButton.text = errors > 0
                ? "Console " + errors
                : warnings > 0 ? "Console " + warnings : "Console";
            consoleButton.tooltip = errors + " error(s), " + warnings + " warning(s). Click to open the console.";
            consoleButton.style.color = new StyleColor(
                errors > 0
                    ? GraphEditorStyles.ErrorColor
                    : warnings > 0 ? GraphEditorStyles.WarningColor : GraphEditorStyles.MutedTextColor);
        }

        private string BuildTargetLabel()
        {
            string label = Session.SelectedLiveTargetLabel;
            return string.IsNullOrEmpty(label) ? "Target" : "Target: " + label;
        }

        private static string FormatSpeed(float speed) =>
            speed.ToString("0.##") + "x";

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

        private void ResetPreview()
        {
            Commands?.Runtime.ResetPreview();
        }

        private void StopPreview()
        {
            Commands?.Runtime.StopPreview();
        }

        private void ShowSpeedMenu()
        {
            if (Commands == null)
                return;

            var menu = new GenericDropdownMenu();
            for (int i = 0; i < SpeedChoices.Length; i++)
            {
                int index = i;
                menu.AddItem(SpeedChoices[i], Mathf.Approximately(Session.PreviewSpeed, SpeedValues[i]), () =>
                    Commands.Runtime.SetSpeed(SpeedValues[index]));
            }

            menu.DropDown(speedButton.worldBound, speedButton, DropdownMenuSizeMode.Content);
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
                    menu.AddItem(Session.LiveTargets[i].Label, index == Session.SelectedLiveTargetIndex,
                        () => Commands.Runtime.SelectTarget(index));
                }
            }

            Button anchor = Session.Mode == GraphEditorMode.LiveNoTarget
                ? noTargetButton
                : targetButton;
            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Content);
        }

        private void ToggleConsole()
        {
            if (Commands == null)
                return;

            if (Session.Console.visible)
            {
                Commands.Layout.ToggleConsole();
                return;
            }

            Commands.Layout.OpenConsole(lastErrorCount > 0
                ? GraphConsoleTab.Validation
                : Session.Console.tab);
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
            System.Action action)
        {
            Button button = GraphEditorStyles.CreateToolButton(text, tooltip, action);
            button.style.marginLeft = GraphEditorMetrics.TinySpacing;
            button.style.marginRight = GraphEditorMetrics.TinySpacing;
            group.Add(button);
            return button;
        }
    }
}
