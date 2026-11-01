using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class SearchService : System.IDisposable
    {
        private readonly GraphEditorSession session;
        private readonly NavigationCommands navigation;
        private GraphSearchProvider provider;

        public SearchService(GraphEditorSession session, NavigationCommands navigation)
        {
            this.session = session;
            this.navigation = navigation;
        }

        public void Open(Vector2 screenPoint)
        {
            if (session.Controller == null)
            {
                session.ReportStatus("Open a graph before searching.", true);
                return;
            }

            if (provider == null)
            {
                provider = ScriptableObject.CreateInstance<GraphSearchProvider>();
                provider.hideFlags = HideFlags.HideAndDontSave;
            }

            provider.Initialize(session.Controller, OnResult);

            SearchWindow.Open(new SearchWindowContext(screenPoint), provider);
        }

        public void Dispose()
        {
            if (provider == null)
                return;

            UnityEngine.Object.DestroyImmediate(provider);
            provider = null;
        }

        private void OnResult(GraphSearchResult result)
        {
            if (result == null)
                return;

            EditorApplication.delayCall += () =>
            {
                navigation.NavigateTo(
                    result.MachineId,
                    result.StateId,
                    result.TransitionId,
                    true);
            };
        }
    }
}
