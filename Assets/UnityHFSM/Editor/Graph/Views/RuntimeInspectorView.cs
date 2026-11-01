using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Runtime tab. In Author it hosts preview settings; in Preview/Live it shows
    /// target, active path and selection-specific live information. Ticks update
    /// labels only, never rebuild the tree.
    /// </summary>
    public sealed class RuntimeInspectorView : GraphEditorView
    {
        private readonly ScrollView content;

        private Label statusLabel;
        private Label targetLabel;
        private Label pathLabel;
        private Label errorLabel;
        private ObjectField hostField;
        private Label selectionTitle;
        private Label selectionStatus;
        private Label selectionDetail;
        private Label conditionSummary;
        private Label lastFired;
        private readonly List<Label> conditionChecks = new List<Label>();

        private ConditionEvaluator evaluator;
        private string selectedTransitionId;
        private bool updatingHost;

        public RuntimeInspectorView()
        {
            style.flexGrow = 1f;
            style.minHeight = 0f;

            content = new ScrollView();
            content.style.flexGrow = 1f;
            content.style.paddingLeft = 8f;
            content.style.paddingRight = 8f;
            content.style.paddingTop = 6f;
            Add(content);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.StructureRebuild
                | GraphEditorChange.ModelDirty
                | GraphEditorChange.Selection
                | GraphEditorChange.Mode
                | GraphEditorChange.Targets
                | GraphEditorChange.All)) != 0)
            {
                Rebuild();
            }

            UpdateLive();
        }

        private void Rebuild()
        {
            content.Clear();
            conditionChecks.Clear();
            evaluator = null;
            selectedTransitionId = null;
            statusLabel = null;
            targetLabel = null;
            pathLabel = null;
            errorLabel = null;
            hostField = null;
            selectionTitle = null;
            selectionStatus = null;
            selectionDetail = null;
            conditionSummary = null;
            lastFired = null;

            if (!Session.HasDocument)
            {
                content.Add(new Label("No graph asset selected."));
                return;
            }

            AddHeader("Runtime");
            statusLabel = new Label();
            content.Add(statusLabel);

            targetLabel = GraphEditorStyles.CreateMutedLabel();
            content.Add(targetLabel);

            pathLabel = GraphEditorStyles.CreateMutedLabel();
            pathLabel.style.whiteSpace = WhiteSpace.Normal;
            content.Add(pathLabel);

            errorLabel = new Label();
            errorLabel.style.whiteSpace = WhiteSpace.Normal;
            errorLabel.style.color = new StyleColor(GraphEditorStyles.ErrorColor);
            errorLabel.style.display = DisplayStyle.None;
            content.Add(errorLabel);

            BuildHostRow();

            if (Session.Mode == GraphEditorMode.Author)
            {
                content.Add(GraphEditorStyles.CreateMutedLabel(
                    "Start Preview from the header bar to inspect the simulation."));
                return;
            }

            if (Session.Mode == GraphEditorMode.LiveNoTarget)
            {
                content.Add(new Label(
                    "No live GraphRuntime matches this graph. Create one in code and tick it; "
                    + "the editor discovers it automatically."));
                return;
            }

            BuildSelectionSection();
        }

        private void BuildHostRow()
        {
            hostField = new ObjectField("Preview Host")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
            };
            hostField.style.flexGrow = 1f;
            hostField.style.maxWidth = 320f;

            GameObject host = Session.Mode == GraphEditorMode.Author
                ? Session.PreviewHost
                : Session.CurrentRuntime != null
                    ? Session.CurrentRuntime.Options.Host
                    : null;

            updatingHost = true;
            hostField.SetValueWithoutNotify(host);
            updatingHost = false;

            hostField.SetEnabled(GraphEditorModePolicy.CanEditPreviewSettings(Session.Mode));
            hostField.tooltip = GraphEditorModePolicy.CanEditPreviewSettings(Session.Mode)
                ? "Optional GameObject passed to StateBehaviourContext.Host during preview."
                : "Stop the runtime to change the preview host.";

            hostField.RegisterValueChangedCallback(evt =>
            {
                if (updatingHost)
                    return;

                GraphCommandResult result = Commands.Runtime.SetPreviewHost(evt.newValue as GameObject);
                if (!result.Success)
                {
                    updatingHost = true;
                    hostField.SetValueWithoutNotify(Session.PreviewHost);
                    updatingHost = false;
                }
            });
            content.Add(hostField);
        }

        private void BuildSelectionSection()
        {
            var spacer = new VisualElement();
            spacer.style.marginTop = GraphEditorMetrics.Spacing;
            content.Add(spacer);

            selectionTitle = new Label();
            selectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            content.Add(selectionTitle);

            selectionStatus = new Label();
            content.Add(selectionStatus);

            selectionDetail = GraphEditorStyles.CreateMutedLabel();
            selectionDetail.style.whiteSpace = WhiteSpace.Normal;
            content.Add(selectionDetail);

            GraphEditorSelection selection = Session.Selection;
            GraphRuntime runtime = Session.CurrentRuntime;

            if (selection.Kind == GraphEditorSelectionKind.State)
            {
                StateDef state = Session.FindState(selection.StateId);
                selectionTitle.text = state != null ? "State: " + state.name : "State";
                selectionDetail.text = state != null && state.kind == StateKind.HybridSubMachine
                    ? "Sub-state machine"
                    : string.Empty;
            }
            else if (selection.Kind == GraphEditorSelectionKind.Transition)
            {
                TransitionDef transition = Session.FindTransition(selection.TransitionId);
                selectionTitle.text = "Transition";
                selectedTransitionId = transition != null ? transition.id : null;

                if (transition != null && runtime != null)
                {
                    try
                    {
                        evaluator = new ConditionEvaluator(runtime.Schema, transition.conditions);
                    }
                    catch (Exception)
                    {
                        evaluator = null;
                    }
                }

                conditionSummary = new Label();
                conditionSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
                content.Add(conditionSummary);

                if (evaluator != null)
                {
                    for (int i = 0; i < evaluator.Count; i++)
                    {
                        var check = new Label();
                        check.style.fontSize = GraphEditorMetrics.SmallFontSize;
                        content.Add(check);
                        conditionChecks.Add(check);
                    }
                }

                lastFired = GraphEditorStyles.CreateMutedLabel();
                content.Add(lastFired);
            }
            else if (selection.Kind == GraphEditorSelectionKind.Multi)
            {
                selectionTitle.text = selection.Items.Count + " items selected";
                selectionDetail.text = "Select one state, transition or machine for details.";
            }
            else
            {
                MachineDef machine = Session.FindMachine(Session.CurrentMachineId);
                selectionTitle.text = machine != null ? "Machine: " + machine.name : "Machine";
            }
        }

        private void UpdateLive()
        {
            if (statusLabel == null)
                return;

            GraphRuntime runtime = Session.CurrentRuntime;
            GraphEditorMode mode = Session.Mode;

            if (mode == GraphEditorMode.Author)
            {
                statusLabel.text = "Not running";
                statusLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                targetLabel.text = "Preview settings";
                pathLabel.text = "Active: (none)";
            }
            else
            {
                string modeText = mode == GraphEditorMode.Preview ? "Preview" : "Live";
                statusLabel.text = runtime == null
                    ? modeText + " / no runtime"
                    : modeText
                        + (Session.IsPaused ? " / Paused" : " / Running")
                        + " / t=" + (Session.Snapshot != null ? Session.Snapshot.time.ToString("0.00") : "0.00");
                statusLabel.style.color = new StyleColor(
                    mode == GraphEditorMode.Preview
                        ? GraphEditorStyles.PreviewColor
                        : GraphEditorStyles.LiveColor);

                targetLabel.text = mode == GraphEditorMode.Preview
                    ? "Target: Preview runtime"
                    : "Target: " + (string.IsNullOrEmpty(Session.SelectedLiveTargetLabel)
                        ? "(none)"
                        : Session.SelectedLiveTargetLabel);
                pathLabel.text = "Active: " + Session.BuildActivePath(Session.Snapshot);
            }

            string error = Session.PreviewLastError;
            errorLabel.text = error ?? string.Empty;
            errorLabel.style.display = string.IsNullOrEmpty(error)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            if (hostField != null
                && GraphEditorModePolicy.CanEditPreviewSettings(mode)
                && !updatingHost
                && hostField.value != Session.PreviewHost)
            {
                updatingHost = true;
                hostField.SetValueWithoutNotify(Session.PreviewHost);
                updatingHost = false;
            }

            if (selectionTitle == null)
                return;

            GraphEditorSelection selection = Session.Selection;
            if (selection.Kind == GraphEditorSelectionKind.State)
                UpdateState(selection.StateId);
            else if (selection.Kind == GraphEditorSelectionKind.Transition)
                UpdateTransition(selection.TransitionId);
            else if (selection.Kind == GraphEditorSelectionKind.Machine)
                UpdateMachine(Session.CurrentMachineId);
            else if (selection.Kind == GraphEditorSelectionKind.Multi)
                UpdateMachine(Session.CurrentMachineId);
        }

        private void UpdateState(string stateId)
        {
            StateDef state = Session.FindState(stateId);
            if (state == null)
                return;

            MachineRuntimeSnapshot machine = FindMachine(Session.Snapshot, state.machineId);
            bool active = machine != null && machine.activeStateId == stateId;
            bool pending = machine != null && machine.pendingStateId == stateId;

            selectionStatus.text = active ? "Active" : pending ? "Pending" : "Inactive";
            selectionStatus.style.color = new StyleColor(
                active ? GraphEditorStyles.SuccessColor
                    : pending ? GraphEditorStyles.WarningColor
                        : GraphEditorStyles.MutedTextColor);

            if (active && machine != null)
            {
                if (state.kind == StateKind.HybridSubMachine)
                {
                    MachineRuntimeSnapshot child = FindMachine(Session.Snapshot, state.subMachineId);
                    selectionDetail.text = child != null && !string.IsNullOrEmpty(child.activeStateName)
                        ? "Child active: " + child.activeStateName
                        : "Child machine is not active.";
                }
                else
                {
                    selectionDetail.text = "Elapsed: " + machine.stateElapsed.ToString("0.###") + " s";
                }
            }
            else if (pending && machine != null)
            {
                selectionDetail.text = machine.isExitPending
                    ? "Pending: Exit"
                    : "Pending: " + machine.pendingStateName;
            }
            else
            {
                selectionDetail.text = string.Empty;
            }
        }

        private void UpdateTransition(string transitionId)
        {
            GraphRuntime runtime = Session.CurrentRuntime;
            if (runtime == null || evaluator == null || transitionId != selectedTransitionId)
            {
                if (conditionSummary != null)
                    conditionSummary.text = "Conditions unavailable (restart the runtime).";
                return;
            }

            try
            {
                bool satisfied = evaluator.Evaluate(runtime.Blackboard);
                conditionSummary.text = satisfied
                    ? "Conditions satisfied"
                    : "Conditions not satisfied";
                conditionSummary.style.color = new StyleColor(
                    satisfied ? GraphEditorStyles.SuccessColor : GraphEditorStyles.MutedTextColor);

                for (int i = 0; i < conditionChecks.Count; i++)
                {
                    Label check = conditionChecks[i];
                    if (i >= evaluator.Count)
                    {
                        check.text = "—";
                        continue;
                    }

                    bool passed = evaluator.EvaluateCondition(i, runtime.Blackboard);
                    check.text = "Condition " + (i + 1) + ": " + (passed ? "OK" : "X");
                    check.style.color = new StyleColor(
                        passed ? GraphEditorStyles.SuccessColor : GraphEditorStyles.ErrorColor);
                }

                TransitionRecord record = FindLastTransition(runtime.TransitionLog, transitionId);
                lastFired.text = record != null
                    ? "Last fired: #" + record.sequence + " at t=" + record.time.ToString("0.###") + " s"
                    : "Last fired: never";
            }
            catch (Exception)
            {
                conditionSummary.text = "Conditions unavailable (runtime schema changed).";
            }
        }

        private void UpdateMachine(string machineId)
        {
            MachineRuntimeSnapshot machine = FindMachine(Session.Snapshot, machineId);
            if (machine == null)
            {
                selectionStatus.text = "Inactive";
                selectionStatus.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                selectionDetail.text = string.Empty;
                return;
            }

            bool active = !string.IsNullOrEmpty(machine.activeStateName);
            selectionStatus.text = active ? "Active: " + machine.activeStateName : "Inactive";
            selectionStatus.style.color = new StyleColor(
                active ? GraphEditorStyles.SuccessColor : GraphEditorStyles.MutedTextColor);
            selectionDetail.text = active
                ? "Elapsed: " + machine.stateElapsed.ToString("0.###") + " s"
                : machine.hasPendingTransition
                    ? machine.isExitPending ? "Pending: Exit" : "Pending: " + machine.pendingStateName
                    : string.Empty;
        }

        private void AddHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = GraphEditorMetrics.Spacing;
            content.Add(label);
        }

        private static MachineRuntimeSnapshot FindMachine(GraphRuntimeSnapshot snapshot, string machineId)
        {
            if (snapshot == null || string.IsNullOrEmpty(machineId))
                return null;

            for (int i = 0; i < snapshot.machines.Count; i++)
            {
                MachineRuntimeSnapshot machine = snapshot.machines[i];
                if (machine != null && machine.machineId == machineId)
                    return machine;
            }

            return null;
        }

        private static TransitionRecord FindLastTransition(
            IReadOnlyList<TransitionRecord> transitions,
            string transitionId)
        {
            if (transitions == null || string.IsNullOrEmpty(transitionId))
                return null;

            for (int i = transitions.Count - 1; i >= 0; i--)
            {
                TransitionRecord record = transitions[i];
                if (record != null && record.transitionId == transitionId)
                    return record;
            }

            return null;
        }
    }
}
