using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Collapsible console with Validation and Runtime Log tabs.</summary>
    public sealed class GraphConsoleView : GraphEditorView
    {
        private readonly Button validationTab;
        private readonly Button runtimeLogTab;
        private readonly Button closeButton;
        private readonly VisualElement content;
        private readonly ValidationView validationView;
        private readonly RuntimeLogView runtimeLogView;

        public GraphConsoleView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 0f;
            style.flexShrink = 0f;
            style.minHeight = 0f;

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.alignItems = Align.Center;
            tabs.style.height = GraphEditorMetrics.ConsoleTabHeight;
            tabs.style.minHeight = GraphEditorMetrics.ConsoleTabHeight;
            tabs.style.maxHeight = GraphEditorMetrics.ConsoleTabHeight;
            tabs.style.flexShrink = 0f;
            tabs.style.paddingLeft = GraphEditorMetrics.Spacing;
            tabs.style.paddingRight = GraphEditorMetrics.Spacing;
            tabs.style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            tabs.style.borderBottomWidth = 1f;
            tabs.style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            Add(tabs);

            validationTab = CreateTab("Validation", () =>
                Commands.Layout.SetConsoleTab(GraphConsoleTab.Validation));
            tabs.Add(validationTab);

            runtimeLogTab = CreateTab("Runtime Log", () =>
                Commands.Layout.SetConsoleTab(GraphConsoleTab.RuntimeLog));
            tabs.Add(runtimeLogTab);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            tabs.Add(spacer);

            closeButton = CreateTab("×", () => Commands.Layout.CloseConsole());
            closeButton.tooltip = "Close the console";
            closeButton.style.minWidth = 24f;
            tabs.Add(closeButton);

            content = new VisualElement();
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            Add(content);

            validationView = new ValidationView();
            validationView.style.flexGrow = 1f;
            validationView.style.minHeight = 0f;
            content.Add(validationView);

            runtimeLogView = new RuntimeLogView();
            runtimeLogView.style.flexGrow = 1f;
            runtimeLogView.style.minHeight = 0f;
            content.Add(runtimeLogView);
        }

        protected override void OnBind()
        {
            validationView.Bind(Session, Commands);
            runtimeLogView.Bind(Session, Commands);
            Apply();
        }

        protected override void OnUnbind()
        {
            validationView.Unbind();
            runtimeLogView.Unbind();
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if ((change & (GraphEditorChange.Console
                | GraphEditorChange.Document
                | GraphEditorChange.Mode
                | GraphEditorChange.All)) != 0)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (Session == null)
                return;

            bool visible = Session.Console.visible;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            GraphConsoleTab tab = Session.Console.tab;
            validationView.style.display = tab == GraphConsoleTab.Validation
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            runtimeLogView.style.display = tab == GraphConsoleTab.RuntimeLog
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            StyleTab(validationTab, Session.Console.tab == GraphConsoleTab.Validation);
            StyleTab(runtimeLogTab, Session.Console.tab == GraphConsoleTab.RuntimeLog);
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

        private static Button CreateTab(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = GraphEditorMetrics.ConsoleTabHeight - 2f;
            button.style.minWidth = 64f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 2f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            button.style.fontSize = GraphEditorMetrics.FontSize;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.flexShrink = 0f;
            return button;
        }
    }
}
