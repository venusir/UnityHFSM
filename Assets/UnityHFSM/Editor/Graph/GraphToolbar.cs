using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Animator-style toolbar: breadcrumb, preview controls, runtime target,
    /// validation badge and view toggles.
    /// </summary>
    public sealed class GraphToolbar : VisualElement
    {
        private static readonly float[] SpeedValues = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly string[] SpeedChoices = { "0.1x", "0.25x", "0.5x", "1x", "2x", "4x" };

        private readonly StateMachineGraphWindow window;
        private readonly VisualElement breadcrumb;
        private readonly Label followStatus;
        private readonly Button findButton;
        private readonly Button playPauseButton;
        private readonly Button stepButton;
        private readonly Button restartButton;
        private readonly DropdownField speedField;
        private readonly DropdownField targetField;
        private readonly Button validationBadge;
        private readonly Button statusBadge;
        private readonly Button overflowButton;
        private readonly Toggle lockToggle;

        private string lastBreadcrumbKey;
        private string lastTargetKey;
        private string followStatusText = string.Empty;
        private bool updatingSpeed;
        private bool updatingTargets;
        private bool updatingLock;

        public GraphToolbar(StateMachineGraphWindow window)
        {
            this.window = window;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = GraphEditorStyles.ToolbarHeight;
            style.minHeight = GraphEditorStyles.ToolbarHeight;
            style.maxHeight = GraphEditorStyles.ToolbarHeight;
            style.flexShrink = 0f;
            style.paddingLeft = 6f;
            style.paddingRight = 6f;
            style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            style.borderBottomWidth = 1f;
            style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            style.overflow = Overflow.Hidden;

            breadcrumb = new VisualElement();
            breadcrumb.style.flexDirection = FlexDirection.Row;
            breadcrumb.style.alignItems = Align.Center;
            breadcrumb.style.flexGrow = 1f;
            breadcrumb.style.flexShrink = 1f;
            breadcrumb.style.minWidth = 0f;
            breadcrumb.style.overflow = Overflow.Hidden;
            Add(breadcrumb);

            followStatus = GraphEditorStyles.CreateStatusLabel();
            followStatus.style.display = DisplayStyle.None;
            followStatus.style.maxWidth = 170f;
            Add(followStatus);

            Add(CreateSeparator());

            findButton = GraphEditorStyles.CreateToolButton("Find", "Search states, machines and transitions (Ctrl+F)");
            findButton.clicked += window.OpenSearch;
            Add(findButton);

            playPauseButton = GraphEditorStyles.CreateToolButton("Play", "Start or pause the preview");
            playPauseButton.clicked += window.TogglePreviewPlayback;
            Add(playPauseButton);

            stepButton = GraphEditorStyles.CreateToolButton("Step", "Advance one logic step");
            stepButton.clicked += window.StepRuntime;
            Add(stepButton);

            restartButton = GraphEditorStyles.CreateToolButton("Restart", "Rebuild the preview from the current graph");
            restartButton.clicked += window.RestartRuntime;
            Add(restartButton);

            speedField = new DropdownField();
            speedField.choices = new List<string>(SpeedChoices);
            speedField.SetValueWithoutNotify(SpeedChoices[3]);
            speedField.style.width = 62f;
            speedField.style.height = 20f;
            speedField.style.marginLeft = 2f;
            speedField.style.marginRight = 2f;
            speedField.style.marginTop = 0f;
            speedField.style.marginBottom = 0f;
            speedField.tooltip = "Preview speed";
            speedField.RegisterValueChangedCallback(evt =>
            {
                if (updatingSpeed)
                    return;

                int index = Array.IndexOf(SpeedChoices, evt.newValue);
                if (index >= 0)
                    window.SetPreviewSpeed(SpeedValues[index]);
            });
            Add(speedField);

            targetField = new DropdownField();
            targetField.style.width = 170f;
            targetField.style.height = 20f;
            targetField.style.marginLeft = 2f;
            targetField.style.marginRight = 2f;
            targetField.style.marginTop = 0f;
            targetField.style.marginBottom = 0f;
            targetField.style.display = DisplayStyle.None;
            targetField.tooltip = "Live runtime to inspect";
            targetField.RegisterValueChangedCallback(evt =>
            {
                if (updatingTargets)
                    return;

                int index = targetField.choices.IndexOf(evt.newValue);
                if (index >= 0)
                    window.SelectRuntimeTarget(index);
            });
            Add(targetField);

            validationBadge = GraphEditorStyles.CreateBadgeButton("0E 0W", "Open validation");
            validationBadge.clicked += window.OpenValidationTab;
            Add(validationBadge);

            statusBadge = GraphEditorStyles.CreateBadgeButton("Idle", "Open runtime log");
            statusBadge.clicked += window.OpenRuntimeLogTab;
            Add(statusBadge);

            overflowButton = GraphEditorStyles.CreateToolButton("⋯", "View options");
            overflowButton.clicked += ShowOverflowMenu;
            Add(overflowButton);

            lockToggle = new Toggle("Lock");
            lockToggle.style.height = 18f;
            lockToggle.style.marginLeft = 4f;
            lockToggle.style.marginRight = 0f;
            lockToggle.style.marginTop = 0f;
            lockToggle.style.marginBottom = 0f;
            lockToggle.style.flexShrink = 0f;
            lockToggle.tooltip = "Keep the current graph when the selection changes.";
            lockToggle.RegisterValueChangedCallback(evt =>
            {
                if (updatingLock)
                    return;

                window.SetSelectionLocked(evt.newValue);
            });
            Add(lockToggle);

            RegisterCallback<GeometryChangedEvent>(_ => UpdateCompactMode());
            UpdateCompactMode();
        }

        public void UpdateBreadcrumb(IReadOnlyList<MachineDef> chain, string currentMachineId)
        {
            var builder = new System.Text.StringBuilder();
            if (chain != null)
            {
                for (int i = 0; i < chain.Count; i++)
                    builder.Append(chain[i].id).Append(':').Append(chain[i].name).Append('|');
            }

            string key = builder.ToString();
            if (key == lastBreadcrumbKey)
                return;

            lastBreadcrumbKey = key;
            breadcrumb.Clear();

            if (chain == null || chain.Count == 0)
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
                    () => window.SetCurrentMachine(machineId));
                button.tooltip = machine.name;

                if (machine.id == currentMachineId)
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                breadcrumb.Add(button);
            }
        }

        public void SetFollowStatus(string text)
        {
            followStatusText = text ?? string.Empty;
            followStatus.text = followStatusText;
            followStatus.tooltip = followStatusText;
            UpdateCompactMode();
        }

        public void SetLock(bool value)
        {
            updatingLock = true;
            lockToggle.SetValueWithoutNotify(value);
            updatingLock = false;
        }

        public void SetValidation(GraphValidationResult result)
        {
            if (result == null)
            {
                validationBadge.text = "—";
                validationBadge.tooltip = "No graph asset selected.";
                validationBadge.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                return;
            }

            int errors = 0;
            int warnings = 0;

            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].severity == GraphIssueSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            validationBadge.text = errors + "E " + warnings + "W";
            validationBadge.tooltip = errors + " error(s), " + warnings + " warning(s). Click to open validation.";
            validationBadge.style.color = new StyleColor(
                errors > 0
                    ? GraphEditorStyles.ErrorColor
                    : warnings > 0
                        ? GraphEditorStyles.WarningColor
                        : GraphEditorStyles.SuccessColor);
        }

        public void SetRuntimeStatus(string text, Color color, string tooltip)
        {
            statusBadge.text = text;
            statusBadge.tooltip = tooltip;
            statusBadge.style.color = new StyleColor(color);
        }

        public void SetRuntimeControls(
            bool playEnabled,
            bool isRunning,
            bool isPaused,
            bool stepEnabled,
            bool restartEnabled,
            bool speedEnabled)
        {
            playPauseButton.SetEnabled(playEnabled);
            playPauseButton.text = isRunning && !isPaused ? "Pause" : "Play";
            playPauseButton.tooltip = isRunning
                ? isPaused ? "Resume the runtime" : "Pause the runtime"
                : "Start the preview";
            stepButton.SetEnabled(stepEnabled);
            restartButton.SetEnabled(restartEnabled);
            speedField.SetEnabled(speedEnabled);
        }

        public void SetSpeed(float speed)
        {
            int index = FindClosestSpeedIndex(speed);
            updatingSpeed = true;
            speedField.SetValueWithoutNotify(SpeedChoices[index]);
            updatingSpeed = false;
        }

        public void SetTargets(IReadOnlyList<string> labels, int selectedIndex, bool visible)
        {
            var builder = new System.Text.StringBuilder();
            if (labels != null)
            {
                for (int i = 0; i < labels.Count; i++)
                    builder.Append(labels[i]).Append('|');
            }

            builder.Append('#').Append(selectedIndex);
            string key = builder.ToString();

            targetField.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (key == lastTargetKey)
                return;

            lastTargetKey = key;
            updatingTargets = true;
            targetField.choices = labels != null ? new List<string>(labels) : new List<string>();

            if (labels != null && selectedIndex >= 0 && selectedIndex < labels.Count)
                targetField.SetValueWithoutNotify(labels[selectedIndex]);
            else
                targetField.SetValueWithoutNotify(string.Empty);

            updatingTargets = false;
            targetField.SetEnabled(visible && labels != null && labels.Count > 0);
        }

        private void ShowOverflowMenu()
        {
            var menu = new GenericDropdownMenu();
            menu.AddItem("Grid", window.IsGridVisible, window.ToggleGrid);
            menu.AddItem("Frame All", false, window.FrameAll);
            menu.AddItem("Center on Selection", false, window.FrameSelection);
            menu.AddItem("Reset Zoom", false, window.ResetZoom);
            menu.AddSeparator(string.Empty);
            menu.AddItem("Parameters Panel", window.IsParametersVisible, window.ToggleParametersPanel);
            menu.AddItem("Inspector Panel", window.IsInspectorVisible, window.ToggleInspectorPanel);
            menu.AddItem("Runtime Log", window.IsRuntimeLogVisible, window.OpenRuntimeLogTab);
            menu.DropDown(overflowButton.worldBound, overflowButton, DropdownMenuSizeMode.Content);
        }

        private void UpdateCompactMode()
        {
            bool compact = layout.width > 0f && layout.width < 760f;

            speedField.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            followStatus.style.display =
                compact || string.IsNullOrEmpty(followStatusText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        private static int FindClosestSpeedIndex(float speed)
        {
            int best = 3;
            float bestDelta = float.MaxValue;

            for (int i = 0; i < SpeedValues.Length; i++)
            {
                float delta = Mathf.Abs(SpeedValues[i] - speed);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }

            return best;
        }

        private static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.width = 1f;
            separator.style.height = 16f;
            separator.style.marginLeft = 5f;
            separator.style.marginRight = 5f;
            separator.style.backgroundColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            separator.style.flexShrink = 0f;
            return separator;
        }
    }
}
