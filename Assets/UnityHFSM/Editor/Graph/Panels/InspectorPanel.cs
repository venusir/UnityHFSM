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

        public InspectorPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            style.width = 320f;
            style.borderLeftWidth = 1f;
            style.borderLeftColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 6f;

            content = new ScrollView();
            content.style.flexGrow = 1f;
            Add(content);
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
        }

        private VisualElement BuildConditionRow(TransitionDef transition, int index)
        {
            ConditionDef condition = transition.conditions[index];
            ParameterDef parameter = window.Controller.FindParameter(condition.parameterId);

            var row = new VisualElement();
            row.style.marginTop = 4f;
            row.style.paddingTop = 4f;
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));

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
