namespace UnityHFSM.Graph.Editor
{
    public sealed class DiagnosticsCommands
    {
        private readonly GraphEditorSession session;
        private readonly NavigationCommands navigation;

        public DiagnosticsCommands(GraphEditorSession session, NavigationCommands navigation)
        {
            this.session = session;
            this.navigation = navigation;
        }

        public void OpenValidation() => session.OpenConsole(GraphConsoleTab.Validation);
        public void OpenRuntimeLog() => session.OpenConsole(GraphConsoleTab.RuntimeLog);
        public void Revalidate() => session.Revalidate();

        public GraphCommandResult OpenIssue(GraphIssue issue)
        {
            if (issue == null)
                return GraphCommandResult.Blocked(GraphCommandBlockReason.InvalidTarget, "No issue is selected.");

            string machineId = GraphNavigation.ResolveMachineIdForIssue(session.Model, issue);
            if (string.IsNullOrEmpty(machineId))
                return GraphCommandResult.Blocked(
                    GraphCommandBlockReason.InvalidTarget,
                    "The issue has no navigable target.");

            return navigation.NavigateTo(machineId, issue.stateId, issue.transitionId, true);
        }

        public GraphCommandResult OpenTransitionRecord(TransitionRecord record)
        {
            if (record == null)
                return GraphCommandResult.Blocked(
                    GraphCommandBlockReason.InvalidTarget,
                    "No transition record is selected.");

            string stateId = !string.IsNullOrEmpty(record.toStateId)
                ? record.toStateId
                : record.fromStateId;

            return navigation.NavigateTo(record.machineId, stateId, record.transitionId, true);
        }
    }
}
