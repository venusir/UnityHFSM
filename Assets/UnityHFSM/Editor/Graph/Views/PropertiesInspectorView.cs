using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Authoring properties; read-only while a runtime is active.</summary>
    public sealed class PropertiesInspectorView : GraphEditorView
    {
        private readonly Label readOnlyBanner;
        private readonly ScrollView content;

        public PropertiesInspectorView()
        {
            style.flexGrow = 1f;
            style.minHeight = 0f;

            readOnlyBanner = new Label(
                "Read-only while previewing or playing. Stop the runtime to edit.");
            readOnlyBanner.style.fontSize = GraphEditorMetrics.FontSize;
            readOnlyBanner.style.whiteSpace = WhiteSpace.Normal;
            readOnlyBanner.style.color = new StyleColor(GraphEditorStyles.WarningColor);
            readOnlyBanner.style.marginLeft = 8f;
            readOnlyBanner.style.marginRight = 8f;
            readOnlyBanner.style.marginTop = 6f;
            readOnlyBanner.style.display = DisplayStyle.None;
            Add(readOnlyBanner);

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
                | GraphEditorChange.Selection
                | GraphEditorChange.Mode
                | GraphEditorChange.All)) == 0)
            {
                return;
            }

            Rebuild();
        }

        private void Rebuild()
        {
            content.Clear();
            bool readOnly = IsReadOnly;
            readOnlyBanner.style.display = readOnly ? DisplayStyle.Flex : DisplayStyle.None;
            content.SetEnabled(!readOnly);

            if (!Session.HasDocument)
            {
                content.Add(new Label("No graph asset selected."));
                return;
            }

            GraphEditorSelection selection = Session.Selection;
            switch (selection.Kind)
            {
                case GraphEditorSelectionKind.State:
                    BuildState(Session.FindState(selection.StateId));
                    break;
                case GraphEditorSelectionKind.Transition:
                    BuildTransition(Session.FindTransition(selection.TransitionId));
                    break;
                case GraphEditorSelectionKind.Multi:
                    BuildMulti(selection);
                    break;
                default:
                    BuildMachine(Session.FindMachine(Session.CurrentMachineId));
                    break;
            }
        }

        private void BuildState(StateDef state)
        {
            if (state == null)
            {
                BuildMachine(Session.FindMachine(Session.CurrentMachineId));
                return;
            }

            AddHeader(state.name);

            var nameField = new TextField("Name")
            {
                value = state.name,
                isDelayed = true,
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                GraphCommandResult result = Commands.Authoring.RenameState(state.id, evt.newValue);
                if (!result.Success)
                    nameField.SetValueWithoutNotify(state.name);
            });
            content.Add(nameField);

            var exitTime = new Toggle("Needs Exit Time") { value = state.needsExitTime };
            exitTime.RegisterValueChangedCallback(evt =>
                Commands.Authoring.SetStateNeedsExitTime(state.id, evt.newValue));
            content.Add(exitTime);

            var ghost = new Toggle("Ghost State") { value = state.isGhostState };
            ghost.RegisterValueChangedCallback(evt =>
                Commands.Authoring.SetStateGhost(state.id, evt.newValue));
            content.Add(ghost);

            if (state.kind == StateKind.HybridSubMachine)
            {
                MachineDef child = Session.FindMachine(state.subMachineId);
                if (child != null)
                {
                    var open = new Button(() => Commands.Navigation.OpenSubMachine(state.id))
                    {
                        text = "Open Sub-State Machine",
                    };
                    open.style.marginTop = GraphEditorMetrics.Spacing;
                    content.Add(open);

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

                var field = new ObjectField
                {
                    objectType = typeof(StateBehaviour),
                    allowSceneObjects = false,
                    value = behaviour,
                };
                field.style.flexGrow = 1f;
                field.RegisterValueChangedCallback(evt =>
                    Commands.Authoring.SetBehaviour(state.id, index, evt.newValue as StateBehaviour));
                row.Add(field);

                var remove = new Button(() => Commands.Authoring.RemoveBehaviour(state.id, index))
                {
                    text = "-",
                };
                row.Add(remove);
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

            var add = new Button(() =>
            {
                if (addField.value is StateBehaviour behaviour)
                {
                    GraphCommandResult result = Commands.Authoring.AddBehaviour(state.id, behaviour);
                    if (result.Success)
                        addField.SetValueWithoutNotify(null);
                }
            })
            {
                text = "Add",
            };
            addRow.Add(add);
            content.Add(addRow);
        }

        private void BuildTransition(TransitionDef transition)
        {
            if (transition == null)
            {
                BuildMachine(Session.FindMachine(Session.CurrentMachineId));
                return;
            }

            AddHeader("Transition");
            content.Add(new Label("From: " + StateName(transition)));
            content.Add(new Label("To: " + TargetName(transition)));

            var delay = new FloatField("Delay (s)")
            {
                value = transition.delaySeconds,
                isDelayed = true,
            };
            delay.RegisterValueChangedCallback(evt =>
                Commands.Authoring.SetTransitionDelay(transition.id, evt.newValue));
            content.Add(delay);

            var force = new Toggle("Force Instantly") { value = transition.forceInstantly };
            force.RegisterValueChangedCallback(evt =>
                Commands.Authoring.SetTransitionForceInstantly(transition.id, evt.newValue));
            content.Add(force);

            var order = new IntegerField("Order")
            {
                value = transition.order,
                isDelayed = true,
            };
            order.RegisterValueChangedCallback(evt =>
                Commands.Authoring.SetTransitionOrder(transition.id, evt.newValue));
            content.Add(order);

            content.Add(new Label("Conditions (AND)"));
            content.Add(GraphEditorStyles.CreateMutedLabel(
                "Any State transitions are evaluated before direct transitions."));

            for (int i = 0; i < transition.conditions.Count; i++)
                content.Add(BuildConditionRow(transition, i));

            var addCondition = new Button(() => Commands.Authoring.AddCondition(transition.id))
            {
                text = "Add Condition",
            };
            addCondition.style.marginTop = GraphEditorMetrics.Spacing;
            addCondition.SetEnabled(Session.Model.parameters.Count > 0);
            addCondition.tooltip = Session.Model.parameters.Count == 0
                ? "Add a parameter before adding a condition."
                : string.Empty;
            content.Add(addCondition);
        }

        private VisualElement BuildConditionRow(TransitionDef transition, int index)
        {
            ConditionDef condition = transition.conditions[index];
            ParameterDef parameter = Session.FindParameter(condition.parameterId);

            var row = new VisualElement();
            row.style.marginTop = GraphEditorMetrics.SmallSpacing;
            row.style.paddingTop = GraphEditorMetrics.SmallSpacing;
            GraphEditorStyles.SetBorder(row, GraphEditorStyles.SeparatorColor, topWidth: 1f);

            var parameters = new List<ParameterDef>();
            var choices = new List<string>();
            for (int i = 0; i < Session.Model.parameters.Count; i++)
            {
                ParameterDef candidate = Session.Model.parameters[i];
                if (candidate == null)
                    continue;

                parameters.Add(candidate);
                choices.Add(candidate.name);
            }

            if (choices.Count == 0)
            {
                row.Add(new Label("No parameters defined."));
                return row;
            }

            var parameterField = new DropdownField("Parameter");
            parameterField.choices = choices;
            if (parameter != null)
                parameterField.SetValueWithoutNotify(parameter.name);
            parameterField.RegisterValueChangedCallback(evt =>
            {
                ParameterDef selected = parameters.Find(candidate => candidate.name == evt.newValue);
                if (selected != null)
                    Commands.Authoring.SetConditionParameter(transition.id, index, selected.id);
            });
            row.Add(parameterField);

            if (parameter == null)
                return row;

            List<string> comparisons = ComparisonChoices(parameter.type);
            var comparisonField = new DropdownField("Comparison");
            comparisonField.choices = comparisons;
            string comparisonName = condition.comparison.ToString();
            if (!comparisons.Contains(comparisonName))
                comparisonName = comparisons[0];
            comparisonField.SetValueWithoutNotify(comparisonName);
            comparisonField.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse(evt.newValue, out ConditionComparison comparison))
                    Commands.Authoring.SetConditionComparison(transition.id, index, comparison);
            });
            row.Add(comparisonField);

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var field = new FloatField("Value")
                    {
                        value = condition.floatValue,
                        isDelayed = true,
                    };
                    field.RegisterValueChangedCallback(evt =>
                        Commands.Authoring.SetConditionFloat(transition.id, index, evt.newValue));
                    row.Add(field);
                    break;
                }
                case BlackboardParameterType.Int:
                {
                    var field = new IntegerField("Value")
                    {
                        value = condition.intValue,
                        isDelayed = true,
                    };
                    field.RegisterValueChangedCallback(evt =>
                        Commands.Authoring.SetConditionInt(transition.id, index, evt.newValue));
                    row.Add(field);
                    break;
                }
            }

            var remove = new Button(() => Commands.Authoring.RemoveCondition(transition.id, index))
            {
                text = "Remove",
            };
            row.Add(remove);
            return row;
        }

        private void BuildMachine(MachineDef machine)
        {
            if (machine == null)
            {
                content.Add(new Label("No machine selected."));
                return;
            }

            AddHeader("Machine: " + machine.name);
            BuildStartStateDropdown(machine);

            if (!string.IsNullOrEmpty(machine.parentStateId))
                content.Add(BuildRememberLastStateToggle(machine));

            int count = 0;
            for (int i = 0; i < Session.Model.states.Count; i++)
            {
                StateDef state = Session.Model.states[i];
                if (state != null && state.machineId == machine.id)
                    count++;
            }

            content.Add(new Label("States: " + count));
        }

        private void BuildMulti(GraphEditorSelection selection)
        {
            AddHeader(selection.Items.Count + " items selected");
            content.Add(GraphEditorStyles.CreateMutedLabel(
                "Multi-selection supports batch delete. Select a single item to edit properties."));

            var delete = new Button(() => Commands.Authoring.DeleteSelection())
            {
                text = "Delete Selection",
            };
            delete.style.marginTop = GraphEditorMetrics.Spacing;
            content.Add(delete);
        }

        private void BuildStartStateDropdown(MachineDef machine)
        {
            var choices = new List<string>();
            var stateIds = new List<string>();

            for (int i = 0; i < Session.Model.states.Count; i++)
            {
                StateDef state = Session.Model.states[i];
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

            StateDef current = Session.FindState(machine.startStateId);
            if (current != null)
                field.SetValueWithoutNotify(current.name);

            field.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index >= 0)
                    Commands.Authoring.SetStartState(machine.id, stateIds[index]);
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
                Commands.Authoring.SetMachineRememberLastState(machine.id, evt.newValue));
            return field;
        }

        private string StateName(TransitionDef transition)
        {
            if (transition.source == TransitionSourceKind.Any)
                return "Any State";

            StateDef state = Session.FindState(transition.fromStateId);
            return state != null ? state.name : "(missing state)";
        }

        private string TargetName(TransitionDef transition)
        {
            if (transition.target == TransitionTargetKind.Exit)
                return "Exit";

            StateDef state = Session.FindState(transition.toStateId);
            return state != null ? state.name : "(missing state)";
        }

        private void AddHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = GraphEditorMetrics.Spacing;
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
                    return new List<string> { ConditionComparison.IsSet.ToString() };
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
