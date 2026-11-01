namespace UnityHFSM.Graph.Editor
{
    public sealed class LayoutCommands
    {
        private readonly GraphEditorSession session;

        public LayoutCommands(GraphEditorSession session)
        {
            this.session = session;
        }

        public void ToggleParametersPanel() => session.ToggleParametersPanel();
        public void ToggleInspectorPanel() => session.ToggleInspectorPanel();
        public void SetParametersWidth(float width) => session.SetParametersWidth(width);
        public void SetInspectorWidth(float width) => session.SetInspectorWidth(width);
        public void SetConsoleHeight(float height) => session.SetConsoleHeight(height);
        public void ToggleConsole() => session.ToggleConsole();
        public void OpenConsole(GraphConsoleTab tab) => session.OpenConsole(tab);
        public void SetConsoleTab(GraphConsoleTab tab) => session.SetConsoleTab(tab);
        public void CloseConsole() => session.CloseConsole();
        public void ToggleGrid() => session.ToggleGrid();
    }
}
