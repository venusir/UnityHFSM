using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Collapsible bottom drawer with Validation and Runtime Log tabs. The
    /// window controls its height and visibility.
    /// </summary>
    public sealed class BottomDrawer : VisualElement
    {
        public enum Tab
        {
            Validation = 0,
            RuntimeLog = 1,
        }

        private readonly Button validationTab;
        private readonly Button runtimeTab;
        private readonly VisualElement content;

        private Tab currentTab = Tab.Validation;

        public event Action<Tab> TabChanged;
        public event Action CollapseRequested;

        public BottomDrawer(StateMachineGraphWindow window)
        {
            style.flexDirection = FlexDirection.Column;
            style.minHeight = 0f;
            style.flexShrink = 0f;

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.alignItems = Align.Center;
            tabs.style.height = GraphEditorStyles.DrawerTabHeight;
            tabs.style.minHeight = GraphEditorStyles.DrawerTabHeight;
            tabs.style.maxHeight = GraphEditorStyles.DrawerTabHeight;
            tabs.style.flexShrink = 0f;
            tabs.style.paddingLeft = 6f;
            tabs.style.paddingRight = 6f;
            tabs.style.borderBottomWidth = 1f;
            tabs.style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            Add(tabs);

            validationTab = CreateTabButton("Validation", () => SetTab(Tab.Validation, true));
            tabs.Add(validationTab);

            runtimeTab = CreateTabButton("Runtime Log", () => SetTab(Tab.RuntimeLog, true));
            tabs.Add(runtimeTab);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            tabs.Add(spacer);

            var collapseButton = CreateTabButton("⌄", () => CollapseRequested?.Invoke());
            collapseButton.tooltip = "Collapse the drawer";
            collapseButton.style.minWidth = 24f;
            tabs.Add(collapseButton);

            content = new VisualElement();
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            Add(content);

            ValidationPanel = new ValidationPanel(window);
            ValidationPanel.style.flexGrow = 1f;
            ValidationPanel.style.minHeight = 0f;
            content.Add(ValidationPanel);

            RuntimeLogPanel = new RuntimeLogPanel(window);
            RuntimeLogPanel.style.flexGrow = 1f;
            RuntimeLogPanel.style.minHeight = 0f;
            content.Add(RuntimeLogPanel);

            SetTab(Tab.Validation, false);
        }

        public ValidationPanel ValidationPanel { get; }
        public RuntimeLogPanel RuntimeLogPanel { get; }

        public Tab CurrentTab => currentTab;

        public void SetTab(Tab tab, bool notify)
        {
            currentTab = tab;
            UpdateTabStyles();

            ValidationPanel.style.display = tab == Tab.Validation
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RuntimeLogPanel.style.display = tab == Tab.RuntimeLog
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (notify)
                TabChanged?.Invoke(tab);
        }

        public void SetValidation(GraphValidationResult result)
        {
            ValidationPanel.SetValidation(result);

            if (result == null)
            {
                validationTab.text = "Validation";
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

            validationTab.text = errors > 0 || warnings > 0
                ? "Validation (" + errors + "E " + warnings + "W)"
                : "Validation";
        }

        private Button CreateTabButton(string text, Action onClick)
        {
            var button = new Button(onClick)
            {
                text = text,
            };
            button.style.height = GraphEditorStyles.DrawerTabHeight - 2f;
            button.style.minWidth = 70f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 2f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.fontSize = 11f;
            button.style.flexShrink = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            return button;
        }

        private void UpdateTabStyles()
        {
            StyleTab(validationTab, currentTab == Tab.Validation);
            StyleTab(runtimeTab, currentTab == Tab.RuntimeLog);
        }

        private static void StyleTab(Button button, bool active)
        {
            button.style.backgroundColor = new StyleColor(
                active
                    ? EditorGUIUtility.isProSkin
                        ? new Color(0.33f, 0.33f, 0.33f)
                        : new Color(0.90f, 0.90f, 0.90f)
                    : Color.clear);
            button.style.color = new StyleColor(
                active
                    ? EditorGUIUtility.isProSkin ? Color.white : Color.black
                    : GraphEditorStyles.MutedTextColor);
            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
        }
    }
}
