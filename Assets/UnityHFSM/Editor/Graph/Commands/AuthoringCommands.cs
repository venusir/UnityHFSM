using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class AuthoringCommands
    {
        private readonly GraphEditorSession session;

        public AuthoringCommands(GraphEditorSession session)
        {
            this.session = session;
        }

        private GraphEditorController Controller => session.Controller;

        public GraphCommandResult BeginLink() => session.BeginLink();

        public GraphCommandResult CancelLink()
        {
            session.CancelLink();
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetLinkSource(string stateId) => session.SetLinkSource(stateId);

        public GraphCommandResult CreateState(Vector2 position)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            string stateId = Controller.AddState(session.CurrentMachineId, position);
            if (string.IsNullOrEmpty(stateId))
                return Blocked("Could not create the state.");

            session.SetSelection(GraphEditorSelection.State(stateId));
            return GraphCommandResult.Ok(stateId);
        }

        public GraphCommandResult CreateSubMachine(Vector2 position)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            string hostStateId = Controller.AddSubMachine(session.CurrentMachineId, position);
            if (string.IsNullOrEmpty(hostStateId))
                return Blocked("Could not create the sub-state machine.");

            session.SetSelection(GraphEditorSelection.State(hostStateId));
            return GraphCommandResult.Ok(hostStateId);
        }

        public GraphCommandResult Connect(GraphConnectionRequest request)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            GraphCommandResult validation = ConnectionValidator.Validate(
                session.Model,
                session.CurrentMachineId,
                request);

            if (!validation.Success)
            {
                session.ReportStatus(validation.Message, true);
                return validation;
            }

            if (request.Source == GraphConnectSourceKind.Entry)
            {
                Controller.SetStartState(session.CurrentMachineId, request.TargetStateId);
                session.SetSelection(GraphEditorSelection.State(request.TargetStateId));
                session.ClearLinkSource();
                session.ReportStatus("Start state updated.", false);
                return GraphCommandResult.Ok(request.TargetStateId);
            }

            TransitionSourceKind source = request.Source == GraphConnectSourceKind.AnyState
                ? TransitionSourceKind.Any
                : TransitionSourceKind.State;

            TransitionTargetKind target = request.Target == GraphConnectTargetKind.Exit
                ? TransitionTargetKind.Exit
                : TransitionTargetKind.State;

            string transitionId = Controller.AddTransition(
                session.CurrentMachineId,
                source,
                request.SourceStateId,
                target,
                request.TargetStateId);

            if (string.IsNullOrEmpty(transitionId))
                return Blocked("Could not create the transition.");

            session.SetSelection(GraphEditorSelection.Transition(transitionId));
            session.ClearLinkSource();
            session.ReportStatus("Transition created.", false);
            return GraphCommandResult.Ok(transitionId);
        }

        public GraphCommandResult CompleteLink(string targetStateId)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (string.IsNullOrEmpty(session.PendingLinkSourceId))
                return Blocked("Select a source state first.");

            return Connect(new GraphConnectionRequest(
                GraphConnectSourceKind.State,
                session.PendingLinkSourceId,
                GraphConnectTargetKind.State,
                targetStateId));
        }

        public GraphCommandResult DeleteSelection()
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (!session.Selection.HasSelection
                || session.Selection.Kind == GraphEditorSelectionKind.Machine)
            {
                return Blocked("Select a state or transition to delete.");
            }

            int deleted = 0;
            for (int i = 0; i < session.Selection.Items.Count; i++)
            {
                GraphEditorSelectionItem item = session.Selection.Items[i];
                switch (item.Kind)
                {
                    case GraphEditorSelectionItemKind.State:
                        Controller.DeleteState(item.Id);
                        deleted++;
                        break;
                    case GraphEditorSelectionItemKind.Transition:
                        Controller.DeleteTransition(item.Id);
                        deleted++;
                        break;
                }
            }

            if (deleted == 0)
                return Blocked("The current selection cannot be deleted.");

            session.SetSelection(GraphEditorSelection.Machine(session.CurrentMachineId));
            return GraphCommandResult.Ok();
        }

        public void MoveState(string stateId, Vector2 position)
        {
            if (!GraphEditorModePolicy.AllowsAuthoring(session.Mode))
                return;

            Controller.MoveState(stateId, position);
        }

        public void MovePseudoNode(PseudoNodeKind kind, Vector2 position)
        {
            if (!GraphEditorModePolicy.AllowsAuthoring(session.Mode))
                return;

            Controller.MovePseudoNode(session.CurrentMachineId, kind, position);
        }

        public GraphCommandResult RenameState(string stateId, string name)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (!Controller.RenameState(stateId, name))
                return InvalidName();

            return GraphCommandResult.Ok();
        }

        public GraphCommandResult RenameParameter(string parameterId, string name)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (!Controller.RenameParameter(parameterId, name))
                return InvalidName();

            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetStateNeedsExitTime(string stateId, bool value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetStateNeedsExitTime(stateId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetStateGhost(string stateId, bool value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetStateGhost(stateId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetStartState(string machineId, string stateId)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetStartState(machineId, stateId);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetMachineRememberLastState(string machineId, bool value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetMachineRememberLastState(machineId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetTransitionDelay(string transitionId, float value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetTransitionDelay(transitionId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetTransitionForceInstantly(string transitionId, bool value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetTransitionForceInstantly(transitionId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetTransitionOrder(string transitionId, int value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetTransitionOrder(transitionId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult AddCondition(string transitionId)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (session.Model.parameters.Count == 0)
            {
                session.ReportStatus("Add a parameter before adding a condition.", true);
                return Blocked("Add a parameter before adding a condition.");
            }

            Controller.AddCondition(transitionId);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult RemoveCondition(string transitionId, int index)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.RemoveCondition(transitionId, index);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetConditionParameter(string transitionId, int index, string parameterId)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetConditionParameter(transitionId, index, parameterId);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetConditionComparison(string transitionId, int index, ConditionComparison comparison)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetConditionComparison(transitionId, index, comparison);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetConditionFloat(string transitionId, int index, float value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetConditionFloat(transitionId, index, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetConditionInt(string transitionId, int index, int value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetConditionInt(transitionId, index, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult AddBehaviour(string stateId, StateBehaviour behaviour)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            if (behaviour == null)
                return Blocked("Select a behaviour asset.");

            Controller.AddBehaviour(stateId, behaviour);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetBehaviour(string stateId, int index, StateBehaviour behaviour)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetBehaviour(stateId, index, behaviour);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult RemoveBehaviour(string stateId, int index)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.RemoveBehaviour(stateId, index);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult AddParameter(BlackboardParameterType type)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            return GraphCommandResult.Ok(Controller.AddParameter(type));
        }

        public GraphCommandResult DeleteParameter(string parameterId)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.DeleteParameter(parameterId);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetParameterFloatDefault(string parameterId, float value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetParameterFloatDefault(parameterId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetParameterIntDefault(string parameterId, int value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetParameterIntDefault(parameterId, value);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult SetParameterBoolDefault(string parameterId, bool value)
        {
            if (!EnsureAuthoring(out GraphCommandResult blocked))
                return blocked;

            Controller.SetParameterBoolDefault(parameterId, value);
            return GraphCommandResult.Ok();
        }

        private bool EnsureAuthoring(out GraphCommandResult result)
        {
            if (!GraphEditorModePolicy.AllowsAuthoring(session.Mode))
            {
                result = GraphCommandResult.Blocked(
                    GraphCommandBlockReason.ReadOnlyMode,
                    "The graph is read-only while previewing or playing.");
                session.ReportStatus(result.Message, true);
                return false;
            }

            if (session.Controller == null)
            {
                result = GraphCommandResult.Blocked(
                    GraphCommandBlockReason.NoDocument,
                    "No graph is loaded.");
                return false;
            }

            result = GraphCommandResult.Ok();
            return true;
        }

        private GraphCommandResult Blocked(string message)
        {
            session.ReportStatus(message, true);
            return GraphCommandResult.Blocked(GraphCommandBlockReason.Unknown, message);
        }

        private GraphCommandResult InvalidName()
        {
            const string message = "Name is empty or already used.";
            session.ReportStatus(message, true);
            return GraphCommandResult.Blocked(GraphCommandBlockReason.InvalidName, message);
        }
    }
}
