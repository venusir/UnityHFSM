using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Row 1: document/follow/lock, navigation history, breadcrumb, search.</summary>
    public sealed class GraphDocumentBar : GraphEditorView
    {
        private readonly Button assetButton;
        private readonly Button lockButton;
        private readonly Button backButton;
        private readonly Button forwardButton;
        private readonly VisualElement breadcrumb;
        private readonly Button findButton;
        private string breadcrumbKey;

        public GraphDocumentBar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = GraphEditorMetrics.DocumentBarHeight;
            style.minHeight = GraphEditorMetrics.DocumentBarHeight;
            style.maxHeight = GraphEditorMetrics.DocumentBarHeight;
            style.flexShrink = 0f;
            style.paddingLeft = GraphEditorMetrics.Spacing;
            style.paddingRight = GraphEditorMetrics.Spacing;
            style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            style.borderBottomWidth = 1f;
            style.borderBottomColor = new StyleColor(GraphEditorStyles.SeparatorColor);

            assetButton = GraphEditorStyles.CreateToolButton("No Graph", "Current graph asset");
            assetButton.style.maxWidth = 240f;
            assetButton.style.minWidth = 90f;
            assetButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            assetButton.style.whiteSpace = WhiteSpace.NoWrap;
            assetButton.style.overflow = Overflow.Hidden;
            assetButton.style.textOverflow = TextOverflow.Ellipsis;
            assetButton.clicked += OnAssetClicked;
            Add(assetButton);

            lockButton = GraphEditorStyles.CreateToolButton("Follow", "Lock/unlock Unity selection following");
            lockButton.clicked += OnLockClicked;
            Add(lockButton);

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

            findButton = GraphEditorStyles.CreateToolButton("Find", "Search states, machines and transitions (Ctrl+F)");
            findButton.clicked += OnFindClicked;
            Add(findButton);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            StateMachineGraphAsset asset = Session.Asset;
            assetButton.text = asset != null ? asset.name : "No Graph";
            assetButton.SetEnabled(asset != null);

            string follow = Session.State.selectionLocked
                ? "Locked"
                : Session.State.followedRunner != null
                    ? "Following " + Session.State.followedRunner.gameObject.name
                    : asset != null ? "Graph selected" : "No graph";
            assetButton.tooltip = asset != null
                ? asset.name + "\n" + follow + "\nClick to select the asset in the Project window."
                : "No graph asset selected.";

            bool canFollow = GraphEditorModePolicy.AllowsDocumentFollow(Session.Mode);
            lockButton.SetEnabled(canFollow);
            lockButton.text = Session.State.selectionLocked ? "Locked" : "Follow";
            lockButton.tooltip = !canFollow
                ? "The document is pinned while a runtime is active."
                : Session.State.selectionLocked
                    ? "Selection following is locked. Click to unlock."
                    : "Follow the selected graph asset or runner. Click to lock.";

            backButton.SetEnabled(Session.History.CanGoBack);
            forwardButton.SetEnabled(Session.History.CanGoForward);
            findButton.SetEnabled(Session.HasDocument);

            RebuildBreadcrumb();
        }

        private void OnAssetClicked()
        {
            if (Session?.Asset == null)
                return;

            Selection.activeObject = Session.Asset;
            EditorGUIUtility.PingObject(Session.Asset);
        }

        private void OnLockClicked()
        {
            if (Commands == null)
                return;

            Commands.Document.SetLock(!Session.State.selectionLocked);
        }

        private void OnFindClicked()
        {
            if (Commands == null || Session == null || !Session.HasDocument)
                return;

            Vector2 point = GUIUtility.GUIToScreenPoint(findButton.worldBound.center);
            Commands.Search.Open(point);
        }

        private void RebuildBreadcrumb()
        {
            if (Session == null)
                return;

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
    }
}
