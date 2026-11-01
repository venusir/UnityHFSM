using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public enum InspectorTab
    {
        Properties = 0,
        Runtime = 1,
    }

    /// <summary>Tabbed inspector host: Properties (authoring) + Runtime.</summary>
    public sealed class InspectorView : GraphEditorView
    {
        private readonly Button propertiesTab;
        private readonly Button runtimeTab;
        private readonly VisualElement content;
        private readonly PropertiesInspectorView propertiesView;
        private readonly RuntimeInspectorView runtimeView;

        private InspectorTab currentTab = InspectorTab.Properties;
        private bool autoSelectedRuntime;

        public InspectorView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 0f;
            style.flexShrink = 0f;
            style.minHeight = 0f;

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.alignItems = Align.Center;
            tabs.style.height = GraphEditorMetrics.PanelHeaderHeight;
            tabs.style.minHeight = GraphEditorMetrics.PanelHeaderHeight;
            tabs.style.maxHeight = GraphEditorMetrics.PanelHeaderHeight;
            tabs.style.flexShrink = 0f;
            tabs.style.paddingLeft = GraphEditorMetrics.Spacing;
            tabs.style.paddingRight = GraphEditorMetrics.Spacing;
            tabs.style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            tabs.style.borderBottomWidth = 1f;
            tabs.style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);
            Add(tabs);

            propertiesTab = CreateTab("Properties", () => SelectTab(InspectorTab.Properties, true));
            tabs.Add(propertiesTab);

            runtimeTab = CreateTab("Runtime", () => SelectTab(InspectorTab.Runtime, true));
            tabs.Add(runtimeTab);

            content = new VisualElement();
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            Add(content);

            propertiesView = new PropertiesInspectorView();
            propertiesView.style.flexGrow = 1f;
            propertiesView.style.minHeight = 0f;
            content.Add(propertiesView);

            runtimeView = new RuntimeInspectorView();
            runtimeView.style.flexGrow = 1f;
            runtimeView.style.minHeight = 0f;
            content.Add(runtimeView);
        }

        protected override void OnBind()
        {
            propertiesView.Bind(Session, Commands);
            runtimeView.Bind(Session, Commands);
            ApplyMode(true);
        }

        protected override void OnUnbind()
        {
            propertiesView.Unbind();
            runtimeView.Unbind();
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null)
                return;

            if ((change & (GraphEditorChange.Mode | GraphEditorChange.Document | GraphEditorChange.All)) != 0)
                ApplyMode(false);
        }

        private void ApplyMode(bool force)
        {
            if (Session == null)
                return;

            bool runtimeMode = GraphEditorModePolicy.ShowsRuntimeDetails(Session.Mode);

            if (runtimeMode && currentTab != InspectorTab.Runtime)
            {
                SelectTab(InspectorTab.Runtime, false);
                autoSelectedRuntime = true;
            }
            else if (!runtimeMode && autoSelectedRuntime)
            {
                SelectTab(InspectorTab.Properties, false);
                autoSelectedRuntime = false;
            }
            else if (force)
            {
                SelectTab(currentTab, false);
            }

            UpdateTabStyles();
        }

        private void SelectTab(InspectorTab tab, bool userInitiated)
        {
            currentTab = tab;
            if (userInitiated)
                autoSelectedRuntime = false;

            bool properties = tab == InspectorTab.Properties;
            propertiesView.style.display = properties ? DisplayStyle.Flex : DisplayStyle.None;
            runtimeView.style.display = properties ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            StyleTab(propertiesTab, currentTab == InspectorTab.Properties);
            StyleTab(runtimeTab, currentTab == InspectorTab.Runtime);
        }

        private static void StyleTab(Button button, bool active)
        {
            button.style.backgroundColor = new StyleColor(
                active
                    ? new Color(GraphEditorStyles.HeaderBackgroundColor.r * 1.2f,
                        GraphEditorStyles.HeaderBackgroundColor.g * 1.2f,
                        GraphEditorStyles.HeaderBackgroundColor.b * 1.2f)
                    : Color.clear);
            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            button.style.color = new StyleColor(
                active
                    ? EditorGUIUtility.isProSkin ? Color.white : Color.black
                    : GraphEditorStyles.MutedTextColor);
        }

        private static Button CreateTab(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = GraphEditorMetrics.PanelHeaderHeight - 2f;
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
