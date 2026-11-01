namespace UnityHFSM.Graph.Editor
{
    public sealed class NavigationCommands
    {
        private readonly GraphEditorSession session;

        public NavigationCommands(GraphEditorSession session)
        {
            this.session = session;
        }

        public IGraphEditorCanvasHost Canvas { get; set; }

        public GraphCommandResult NavigateTo(
            string machineId,
            string stateId,
            string transitionId,
            bool pushHistory = true)
        {
            GraphCommandResult result = session.NavigateTo(
                machineId,
                stateId,
                transitionId,
                pushHistory);

            if (!result.Success)
                session.ReportStatus(result.Message, true);
            else
                Canvas?.FrameSelection();

            return result;
        }

        public GraphCommandResult Back()
        {
            GraphCommandResult result = session.Back();
            if (!result.Success)
                session.ReportStatus(result.Message, true);
            else
                Canvas?.FrameSelection();
            return result;
        }

        public GraphCommandResult Forward()
        {
            GraphCommandResult result = session.Forward();
            if (!result.Success)
                session.ReportStatus(result.Message, true);
            else
                Canvas?.FrameSelection();
            return result;
        }

        public GraphCommandResult OpenSubMachine(string hostStateId)
        {
            GraphCommandResult result = session.OpenSubMachine(hostStateId);
            if (!result.Success)
                session.ReportStatus(result.Message, true);
            else
                Canvas?.FrameSelection();
            return result;
        }

        public void Select(GraphEditorSelection selection, bool fromCanvas = false)
        {
            session.SetSelection(selection, fromCanvas);
        }

        public void ClearSelection()
        {
            session.ClearSelection();
        }

        public void FrameAll()
        {
            Canvas?.FrameAll();
        }

        public void FrameSelection()
        {
            Canvas?.FrameSelection();
        }

        public void ResetZoom()
        {
            Canvas?.ResetZoom();
        }

        public void ToggleGrid()
        {
            session.ToggleGrid();
            Canvas?.SetGridVisible(session.GridVisible);
        }
    }
}
