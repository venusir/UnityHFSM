using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class InspectorPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly ScrollView content;

        private string selectedStateId;
        private string selectedTransitionId;

        private VisualElement liveSection;
        private Label liveStatusLabel;
        private Label liveDetailLabel;
        private Label liveElapsedLabel;
        private readonly List<Label> conditionChecks = new List<Label>();
        private ConditionEvaluator conditionEvaluator;
        private string selectedLiveStateId;
        private string selectedLiveMachineId;
        private string selectedLiveTransitionId;

        public InspectorPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            style.width = 320f;
            GraphEditorStyles.SetBorder(this, GraphEditorStyles.SeparatorColor, leftWidth: 1f);

            Add(GraphEditorStyles.CreateHeaderBand("Inspector", out _));

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 8f;
            body.style.paddingRight = 8f;
            body.style.paddingTop = 6f;
            Add(body);

            content = new ScrollView();
            content.style.flexGrow = 1f;
            body.Add(content);
            Refresh();
        }

        public void ShowState(string stateId)
        {
            selectedStateId = stateId;
            selectedTransitionId = null;
            Refresh();
        }

        public void ShowTransition(string transitionId)
        {
            selectedStateId = null;
            selectedTransitionId = transitionId;
            Refresh();
        }

        public void ShowMachine()
        {
            selectedStateId = null;
            selectedTransitionId = null;
            Refresh();
        }

        public void Refresh()
        {
            content.Clear();

            liveSection = null;
            liveStatusLabel = null;
            liveDetailLabel = null;
            liveElapsedLabel = null;
            conditionChecks.Clear();
            conditionEvaluator = null;
            selectedLiveStateId = null;
            selectedLiveMachineId = null;
            selectedLiveTransitionId = null;

            if (window.Controller == null || window.Controller.Model == null)
            {
                content.Add(new Label("No graph asset selected."));
                return;
            }

            if (!string.IsNullOrEmpty(selectedTransitionId))
            {
                TransitionDef transition = window.Controller.FindTransition(selectedTransitionId);
                if (transition != null)
                {
                    BuildTransition(transition);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(selectedStateId))
            {
                StateDef state = window.Controller.FindState(selectedStateId);
                if (state != null)
                {
                    BuildState(state);
                    return;
                }
            }

            BuildMachine(window.CurrentMachineId);
        }

        private void BuildState(StateDef state)
        {
            AddHeader(state.name);

            var nameField = new TextField("Name")
            {
                value = state.name,
                isDelayed = true,
            };
            nameField.RegisterValueChangedCallback(evt =>
                window.Controller?.RenameState(state.id, evt.newValue));
            content.Add(nameField);

            var exitTimeField = new Toggle("Needs Exit Time")
            {
                value = state.needsExitTime,
            };
            exitTimeField.RegisterValueChangedCallback(evt =>
                window.Controller?.SetStateNeedsExitTime(state.id, evt.newValue));
            content.Add(exitTimeField);

            var ghostField = new Toggle("Ghost State")
            {
                value = state.isGhostState,
            };
            ghostField.RegisterValueChangedCallback(evt =>
                window.Controller?.SetStateGhost(state.id, evt.newValue));
            content.Add(ghostField);

            if (state.kind == StateKind.HybridSubMachine)
            {
                MachineDef child = window.Controller.FindMachine(state.subMachineId);
                if (child != null)
                {
                    var openButton = new Button(() => window.SetCurrentMachine(child.id))
                    {
                        text = "Open Sub-State Machine",
                    };
                    openButton.style.marginTop = 6f;
                    content.Add(openButton);

                    BuildStartStateDropdown(child);
                    content.Add(BuildRememberLastStateToggle(child));
                }
            }

            content.Add(new Label("Behaviours"));

            for (int i = 0; i < state.behaviours.Count; i++)
            {
                int index = i;
                StateBehaviour behaviour = state.behaviours[i];

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var objectField = new ObjectField
                {
                    objectType = typeof(StateBehaviour),
                    allowSceneObjects = false,
                    value = behaviour,
                };
                objectField.style.flexGrow = 1f;
                objectField.RegisterValueChangedCallback(evt =>
                    window.Controller?.SetBehaviour(state.id, index, evt.newValue as StateBehaviour));
                row.Add(objectField);

                var removeButton = new Button(() =>
                {
                    window.Controller?.RemoveBehaviour(state.id, index);
                })
                {
                    text = "-",
                };
                row.Add(removeButton);
                content.Add(row);
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;

            var addField = new ObjectField
            {
                objectType = typeof(StateBehaviour),
                allowSceneObjects = false,
            };
            addField.style.flexGrow = 1f;
            addRow.Add(addField);

            var addButton = new Button(() =>
            {
                if (addField.value is StateBehaviour behaviour)
                {
                    window.Controller?.AddBehaviour(state.id, behaviour);
                    addField.value = null;
                }
            })
            {
                text = "Add",
            };
            addRow.Add(addButton);
            content.Add(addRow);

            AddStateLiveSection(state);
        }

        private void BuildTransition(TransitionDef transition)
        {
            AddHeader("Transition");

            content.Add(new Label("From: " + StateName(transition.source == TransitionSourceKind.Any
                ? string.Empty
                : transition.fromStateId, true)));
            content.Add(new Label("To: " + (transition.target == TransitionTargetKind.Exit
                ? "Exit"
                : StateName(transition.toStateId, false))));

            var delayField = new FloatField("Delay (s)")
            {
                value = transition.delaySeconds,
                isDelayed = true,
            };
            delayField.RegisterValueChangedCallback(evt =>
                window.Controller?.SetTransitionDelay(transition.id, evt.newValue));
            content.Add(delayField);

            var forceField = new Toggle("Force Instantly")
            {
                value = transition.forceInstantly,
            };
            forceField.RegisterValueChangedCallback(evt =>
                window.Controller?.SetTransitionForceInstantly(transition.id, evt.newValue));
            content.Add(forceField);

            var orderField = new IntegerField("Order")
            {
                value = transition.order,
                isDelayed = true,
            };
            orderField.RegisterValueChangedCallback(evt =>
                window.Controller?.SetTransitionOrder(transition.id, evt.newValue));
            content.Add(orderField);

            content.Add(new Label("Conditions (AND)"));
            content.Add(new Label("Any State transitions are evaluated before direct transitions."));

            for (int i = 0; i < transition.conditions.Count; i++)
            {
                int index = i;
                content.Add(BuildConditionRow(transition, index));
            }

            var addCondition = new Button(() => window.Controller?.AddCondition(transition.id))
            {
                text = "Add Condition",
            };
            addCondition.style.marginTop = 6f;
            content.Add(addCondition);

            AddTransitionLiveSection(transition);
        }

        private VisualElement BuildConditionRow(TransitionDef transition, int index)
        {
            ConditionDef condition = transition.conditions[index];
            ParameterDef parameter = window.Controller.FindParameter(condition.parameterId);

            var row = new VisualElement();
            row.style.marginTop = 4f;
            row.style.paddingTop = 4f;
            GraphEditorStyles.SetBorder(row, GraphEditorStyles.SeparatorColor, topWidth: 1f);

            if (window.CurrentRuntime != null)
            {
                var check = new Label("—");
                check.style.fontSize = 10f;
                check.style.unityFontStyleAndWeight = FontStyle.Bold;
                check.style.marginTop = 2f;
                check.tooltip = "Live condition result";
                row.Add(check);
                conditionChecks.Add(check);
            }

            var parameterChoices = new List<string>();
            var parameters = new List<ParameterDef>();

            for (int i = 0; i < window.Controller.Model.parameters.Count; i++)
            {
                ParameterDef candidate = window.Controller.Model.parameters[i];
                if (candidate == null)
                    continue;

                parameters.Add(candidate);
                parameterChoices.Add(candidate.name);
            }

            if (parameterChoices.Count == 0)
            {
                row.Add(new Label("No parameters defined."));
                return row;
            }

            var parameterField = new DropdownField("Parameter");
            parameterField.choices = parameterChoices;
            if (parameter != null)
                parameterField.SetValueWithoutNotify(parameter.name);
            parameterField.RegisterValueChangedCallback(evt =>
            {
                ParameterDef selected = parameters.Find(p => p.name == evt.newValue);
                if (selected != null)
                    window.Controller?.SetConditionParameter(transition.id, index, selected.id);
            });
            row.Add(parameterField);

            if (parameter == null)
                return row;

            var comparisonChoices = ComparisonChoices(parameter.type);
            var comparisonField = new DropdownField("Comparison");
            comparisonField.choices = comparisonChoices;
            string comparisonName = condition.comparison.ToString();
            if (!comparisonChoices.Contains(comparisonName))
                comparisonName = comparisonChoices[0];
            comparisonField.SetValueWithoutNotify(comparisonName);
            comparisonField.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse(evt.newValue, out ConditionComparison comparison))
                    window.Controller?.SetConditionComparison(transition.id, index, comparison);
            });
            row.Add(comparisonField);

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var valueField = new FloatField("Value")
                    {
                        value = condition.floatValue,
                        isDelayed = true,
                    };
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetConditionFloat(transition.id, index, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Int:
                {
                    var valueField = new IntegerField("Value")
                    {
                        value = condition.intValue,
                        isDelayed = true,
                    };
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetConditionInt(transition.id, index, evt.newValue));
                    row.Add(valueField);
                    break;
                }
            }

            var removeButton = new Button(() =>
                window.Controller?.RemoveCondition(transition.id, index))
            {
                text = "Remove",
            };
            row.Add(removeButton);
            return row;
        }

        private void BuildMachine(string machineId)
        {
            MachineDef machine = window.Controller.FindMachine(machineId);
            if (machine == null)
            {
                content.Add(new Label("No machine selected."));
                return;
            }

            AddHeader("Machine: " + machine.name);
            BuildStartStateDropdown(machine);

            if (!string.IsNullOrEmpty(machine.parentStateId))
                content.Add(BuildRememberLastStateToggle(machine));

            content.Add(new Label("States: " + CountStates(machine.id)));

            AddMachineLiveSection(machine);
        }

        private void BuildStartStateDropdown(MachineDef machine)
        {
            var choices = new List<string>();
            var stateIds = new List<string>();

            for (int i = 0; i < window.Controller.Model.states.Count; i++)
            {
                StateDef state = window.Controller.Model.states[i];
                if (state == null || state.machineId != machine.id)
                    continue;

                choices.Add(state.name);
                stateIds.Add(state.id);
            }

            if (choices.Count == 0)
            {
                content.Add(new Label("The machine has no states."));
                return;
            }

            var field = new DropdownField("Start State");
            field.choices = choices;

            StateDef current = window.Controller.FindState(machine.startStateId);
            if (current != null)
                field.SetValueWithoutNotify(current.name);

            field.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index >= 0)
                    window.Controller?.SetStartState(machine.id, stateIds[index]);
            });
            content.Add(field);
        }

        private VisualElement BuildRememberLastStateToggle(MachineDef machine)
        {
            var field = new Toggle("Remember Last State")
            {
                value = machine.rememberLastState,
            };
            field.RegisterValueChangedCallback(evt =>
                window.Controller?.SetMachineRememberLastState(machine.id, evt.newValue));
            return field;
        }

        private string StateName(string stateId, bool isAny)
        {
            if (isAny)
                return "Any State";

            StateDef state = window.Controller.FindState(stateId);
            return state != null ? state.name : "(missing state)";
        }

        private int CountStates(string machineId)
        {
            int count = 0;
            for (int i = 0; i < window.Controller.Model.states.Count; i++)
            {
                StateDef state = window.Controller.Model.states[i];
                if (state != null && state.machineId == machineId)
                    count++;
            }

            return count;
        }

        public void UpdateLive(GraphRuntimeSnapshot snapshot)
        {
            if (liveSection == null || snapshot == null)
                return;

            if (!string.IsNullOrEmpty(selectedLiveTransitionId))
                UpdateTransitionLive(snapshot);
            else if (!string.IsNullOrEmpty(selectedLiveStateId))
                UpdateStateLive(snapshot);
            else if (!string.IsNullOrEmpty(selectedLiveMachineId))
                UpdateMachineLive(snapshot);
        }

        private void AddStateLiveSection(StateDef state)
        {
            if (window.CurrentRuntime == null)
                return;

            AddLiveSection("Runtime");
            selectedLiveStateId = state.id;
            selectedLiveMachineId = state.machineId;
            UpdateLive(window.CurrentSnapshot);
        }

        private void AddTransitionLiveSection(TransitionDef transition)
        {
            if (window.CurrentRuntime == null)
                return;

            try
            {
                conditionEvaluator = new ConditionEvaluator(
                    window.CurrentRuntime.Schema,
                    transition.conditions);
            }
            catch (Exception)
            {
                conditionEvaluator = null;
            }

            AddLiveSection("Runtime");
            selectedLiveTransitionId = transition.id;
            UpdateLive(window.CurrentSnapshot);
        }

        private void AddMachineLiveSection(MachineDef machine)
        {
            if (window.CurrentRuntime == null)
                return;

            AddLiveSection("Runtime");
            selectedLiveMachineId = machine.id;
            UpdateLive(window.CurrentSnapshot);
        }

        private void AddLiveSection(string title)
        {
            liveSection = new VisualElement();
            liveSection.style.marginTop = 10f;
            liveSection.style.paddingTop = 6f;
            GraphEditorStyles.SetBorder(
                liveSection,
                GraphEditorStyles.SeparatorColor,
                topWidth: 1f);

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4f;
            liveSection.Add(header);

            liveStatusLabel = new Label();
            liveStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            liveSection.Add(liveStatusLabel);

            liveElapsedLabel = new Label();
            liveElapsedLabel.style.fontSize = 11f;
            liveElapsedLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            liveSection.Add(liveElapsedLabel);

            liveDetailLabel = new Label();
            liveDetailLabel.style.fontSize = 11f;
            liveDetailLabel.style.whiteSpace = WhiteSpace.Normal;
            liveDetailLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            liveSection.Add(liveDetailLabel);

            content.Add(liveSection);
        }

        private void UpdateStateLive(GraphRuntimeSnapshot snapshot)
        {
            MachineRuntimeSnapshot machine = FindMachine(snapshot, selectedLiveMachineId);
            bool active = machine != null && machine.activeStateId == selectedLiveStateId;
            bool pending = machine != null && machine.pendingStateId == selectedLiveStateId;

            if (active)
            {
                SetLiveStatus("Active", GraphEditorStyles.SuccessColor);
                liveElapsedLabel.text = "Elapsed: " + machine.stateElapsed.ToString("0.###") + " s";
            }
            else if (pending)
            {
                SetLiveStatus("Pending", GraphEditorStyles.WarningColor);
                liveElapsedLabel.text = string.Empty;
            }
            else
            {
                SetLiveStatus("Inactive", GraphEditorStyles.MutedTextColor);
                liveElapsedLabel.text = string.Empty;
            }

            StateDef state = window.Controller != null ? window.Controller.FindState(selectedLiveStateId) : null;
            if (state != null && state.kind == StateKind.HybridSubMachine && active)
            {
                MachineRuntimeSnapshot child = FindMachine(snapshot, state.subMachineId);
                liveDetailLabel.text = child != null && !string.IsNullOrEmpty(child.activeStateName)
                    ? "Child active: " + child.activeStateName
                    : "Child machine is not active.";
            }
            else if (pending)
            {
                liveDetailLabel.text = machine.isExitPending
                    ? "Transition pending: Exit"
                    : "Transition pending: " + machine.pendingStateName;
            }
            else
            {
                liveDetailLabel.text = string.Empty;
            }
        }

        private void UpdateTransitionLive(GraphRuntimeSnapshot snapshot)
        {
            GraphRuntime runtime = window.CurrentRuntime;
            if (runtime == null)
            {
                SetLiveStatus("No runtime", GraphEditorStyles.MutedTextColor);
                return;
            }

            TransitionDef transition = window.Controller != null
                ? window.Controller.FindTransition(selectedLiveTransitionId)
                : null;

            if (conditionEvaluator == null || transition == null)
            {
                SetLiveStatus("Conditions unavailable", GraphEditorStyles.WarningColor);
                liveElapsedLabel.text = string.Empty;
                liveDetailLabel.text = "Restart the preview to evaluate this transition.";
                return;
            }

            bool satisfied;
            try
            {
                satisfied = conditionEvaluator.Evaluate(runtime.Blackboard);
            }
            catch (Exception)
            {
                SetLiveStatus("Conditions unavailable", GraphEditorStyles.WarningColor);
                liveElapsedLabel.text = string.Empty;
                liveDetailLabel.text = "The runtime schema no longer matches this transition.";
                return;
            }

            SetLiveStatus(
                satisfied ? "Conditions satisfied" : "Conditions not satisfied",
                satisfied ? GraphEditorStyles.SuccessColor : GraphEditorStyles.MutedTextColor);

            for (int i = 0; i < conditionChecks.Count; i++)
            {
                Label check = conditionChecks[i];
                if (check == null)
                    continue;

                if (i >= conditionEvaluator.Count)
                {
                    check.text = "—";
                    check.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                    continue;
                }

                bool passed;
                try
                {
                    passed = conditionEvaluator.EvaluateCondition(i, runtime.Blackboard);
                }
                catch (Exception)
                {
                    check.text = "—";
                    check.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                    continue;
                }

                check.text = passed ? "OK" : "X";
                check.style.color = new StyleColor(
                    passed ? GraphEditorStyles.SuccessColor : GraphEditorStyles.ErrorColor);
            }

            TransitionRecord last = FindLastTransition(runtime.TransitionLog, selectedLiveTransitionId);
            liveElapsedLabel.text = last != null
                ? "Last fired: #" + last.sequence + " at t=" + last.time.ToString("0.###") + " s"
                : "Last fired: never";
            liveDetailLabel.text = string.Empty;
        }

        private void UpdateMachineLive(GraphRuntimeSnapshot snapshot)
        {
            MachineRuntimeSnapshot machine = FindMachine(snapshot, selectedLiveMachineId);
            if (machine == null)
            {
                SetLiveStatus("Inactive", GraphEditorStyles.MutedTextColor);
                liveElapsedLabel.text = string.Empty;
                liveDetailLabel.text = string.Empty;
                return;
            }

            bool hasActiveState = !string.IsNullOrEmpty(machine.activeStateName);

            SetLiveStatus(
                hasActiveState ? "Active: " + machine.activeStateName : "Inactive",
                hasActiveState
                    ? GraphEditorStyles.SuccessColor
                    : GraphEditorStyles.MutedTextColor);

            liveElapsedLabel.text = hasActiveState
                ? "Elapsed: " + machine.stateElapsed.ToString("0.###") + " s"
                : string.Empty;

            if (machine.hasPendingTransition)
            {
                liveDetailLabel.text = machine.isExitPending
                    ? "Pending: Exit"
                    : "Pending: " + machine.pendingStateName;
            }
            else
            {
                liveDetailLabel.text = string.Empty;
            }
        }

        private void SetLiveStatus(string text, Color color)
        {
            liveStatusLabel.text = text;
            liveStatusLabel.style.color = new StyleColor(color);
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

        private void AddHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6f;
            content.Add(label);
        }

        private static List<string> ComparisonChoices(BlackboardParameterType type)
        {
            switch (type)
            {
                case BlackboardParameterType.Bool:
                    return new List<string>
                    {
                        ConditionComparison.IsTrue.ToString(),
                        ConditionComparison.IsFalse.ToString(),
                    };

                case BlackboardParameterType.Trigger:
                    return new List<string>
                    {
                        ConditionComparison.IsSet.ToString(),
                    };

                default:
                    return new List<string>
                    {
                        ConditionComparison.Equal.ToString(),
                        ConditionComparison.NotEqual.ToString(),
                        ConditionComparison.Less.ToString(),
                        ConditionComparison.LessOrEqual.ToString(),
                        ConditionComparison.Greater.ToString(),
                        ConditionComparison.GreaterOrEqual.ToString(),
                    };
            }
        }
    }
}
