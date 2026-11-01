using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Defaults in Author mode; live values in Preview/Live.</summary>
    public sealed class ParametersView : GraphEditorView
    {
        private sealed class Row
        {
            public ParameterDef Definition;
            public string RuntimeName;
            public BlackboardParameterType Type;
            public TextField NameField;
            public FloatField FloatField;
            public IntegerField IntField;
            public Toggle BoolField;
            public Label TriggerState;
        }

        private readonly ScrollView list;
        private readonly Button addButton;
        private readonly Label modeLabel;
        private readonly List<Row> rows = new List<Row>();

        public ParametersView()
        {
            style.flexGrow = 0f;
            style.flexShrink = 0f;

            var header = GraphEditorStyles.CreateHeaderBand("Parameters", out VisualElement right);
            Add(header);

            modeLabel = GraphEditorStyles.CreateMutedLabel();
            modeLabel.style.marginRight = GraphEditorMetrics.SmallSpacing;
            right.Add(modeLabel);

            addButton = new Button(ShowAddMenu) { text = "+" };
            addButton.tooltip = "Add parameter";
            addButton.style.width = 22f;
            addButton.style.height = GraphEditorMetrics.SmallControlHeight;
            addButton.style.marginLeft = 0f;
            addButton.style.marginRight = 0f;
            addButton.style.marginTop = 0f;
            addButton.style.marginBottom = 0f;
            right.Add(addButton);

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = GraphEditorMetrics.Spacing;
            body.style.paddingRight = GraphEditorMetrics.Spacing;
            body.style.paddingTop = GraphEditorMetrics.Spacing;
            Add(body);

            list = new ScrollView();
            list.style.flexGrow = 1f;
            body.Add(list);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.StructureRebuild
                | GraphEditorChange.Mode
                | GraphEditorChange.Targets
                | GraphEditorChange.All)) != 0)
            {
                RefreshRows();
            }

            UpdateLiveValues();
            UpdateModeLabel();
        }

        private void RefreshRows()
        {
            list.Clear();
            rows.Clear();

            addButton.SetEnabled(IsAuthoring && Session.HasDocument);

            if (!Session.HasDocument)
            {
                list.Add(new Label("No graph asset selected."));
                return;
            }

            if (Session.Model.parameters.Count == 0)
            {
                list.Add(new Label("No parameters. Use + to add one."));
                return;
            }

            for (int i = 0; i < Session.Model.parameters.Count; i++)
            {
                ParameterDef parameter = Session.Model.parameters[i];
                if (parameter != null)
                    list.Add(CreateRow(parameter));
            }
        }

        private VisualElement CreateRow(ParameterDef parameter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = GraphEditorMetrics.ControlHeight;
            row.style.paddingLeft = 2f;
            row.style.paddingRight = 2f;
            GraphEditorStyles.SetBorder(row, GraphEditorStyles.SeparatorColor, bottomWidth: 1f);

            GraphRuntime runtime = Session.CurrentRuntime;
            bool live = runtime != null && !runtime.IsDisposed;
            bool inRuntime = false;
            string runtimeName = null;

            if (live && runtime.Schema.TryGetIndexById(parameter.id, out int runtimeIndex))
            {
                BlackboardParameterInfo info = runtime.Schema.GetParameter(runtimeIndex);
                if (info != null && info.Type == parameter.type)
                {
                    inRuntime = true;
                    runtimeName = info.Name;
                }
            }

            var binding = new Row
            {
                Definition = parameter,
                RuntimeName = runtimeName,
                Type = parameter.type,
            };

            var nameField = new TextField
            {
                value = parameter.name,
                isDelayed = true,
            };
            nameField.style.flexGrow = 1f;
            nameField.style.flexShrink = 1f;
            nameField.style.marginRight = GraphEditorMetrics.SmallSpacing;
            nameField.SetEnabled(IsAuthoring);
            nameField.RegisterValueChangedCallback(evt =>
            {
                GraphCommandResult result = Commands.Authoring.RenameParameter(parameter.id, evt.newValue);
                if (!result.Success)
                    nameField.SetValueWithoutNotify(parameter.name);
            });
            row.Add(nameField);
            binding.NameField = nameField;

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var field = new FloatField
                    {
                        value = live && inRuntime ? runtime.GetFloat(runtimeName) : parameter.defaultFloat,
                        isDelayed = true,
                    };
                    field.style.width = 64f;
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetFloat(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterFloatDefault(parameter.id, evt.newValue);
                    });
                    row.Add(field);
                    binding.FloatField = field;
                    break;
                }

                case BlackboardParameterType.Int:
                {
                    var field = new IntegerField
                    {
                        value = live && inRuntime ? runtime.GetInt(runtimeName) : parameter.defaultInt,
                        isDelayed = true,
                    };
                    field.style.width = 64f;
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetInt(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterIntDefault(parameter.id, evt.newValue);
                    });
                    row.Add(field);
                    binding.IntField = field;
                    break;
                }

                case BlackboardParameterType.Bool:
                {
                    var field = new Toggle
                    {
                        value = live && inRuntime ? runtime.GetBool(runtimeName) : parameter.defaultBool,
                    };
                    field.style.width = 20f;
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetBool(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterBoolDefault(parameter.id, evt.newValue);
                    });
                    row.Add(field);
                    binding.BoolField = field;
                    break;
                }

                case BlackboardParameterType.Trigger:
                {
                    if (live && inRuntime)
                    {
                        var set = new Button(() => Commands.Runtime.SetTrigger(runtimeName))
                        {
                            text = "Set",
                        };
                        set.style.height = GraphEditorMetrics.SmallControlHeight;
                        row.Add(set);

                        var reset = new Button(() => Commands.Runtime.ResetTrigger(runtimeName))
                        {
                            text = "Reset",
                        };
                        reset.style.height = GraphEditorMetrics.SmallControlHeight;
                        row.Add(reset);

                        var state = GraphEditorStyles.CreateMutedLabel("Reset");
                        state.style.width = 36f;
                        state.style.marginLeft = GraphEditorMetrics.TinySpacing;
                        row.Add(state);
                        binding.TriggerState = state;
                    }
                    else
                    {
                        var type = GraphEditorStyles.CreateMutedLabel("Trigger");
                        type.style.opacity = 0.7f;
                        row.Add(type);
                    }

                    break;
                }
            }

            if (live)
            {
                row.tooltip = inRuntime
                    ? "Live value. Changes affect the running graph."
                    : "Not present in the running graph. Stop and restart to apply.";
            }
            else if (IsAuthoring)
            {
                row.tooltip = "Right-click to delete.";
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Delete", _ =>
                        Commands.Authoring.DeleteParameter(parameter.id));
                }));
            }

            rows.Add(binding);
            return row;
        }

        private void UpdateLiveValues()
        {
            GraphRuntimeSnapshot snapshot = Session.Snapshot;
            if (snapshot == null || rows.Count == 0)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                BlackboardParameterSnapshot value = FindParameter(snapshot, row.Definition.id, row.RuntimeName);
                if (value == null || value.type != row.Type)
                    continue;

                switch (row.Type)
                {
                    case BlackboardParameterType.Float:
                        SetValueIfNotFocused(row.FloatField, value.floatValue);
                        break;
                    case BlackboardParameterType.Int:
                        SetValueIfNotFocused(row.IntField, value.intValue);
                        break;
                    case BlackboardParameterType.Bool:
                        SetValueIfNotFocused(row.BoolField, value.boolValue);
                        break;
                    case BlackboardParameterType.Trigger:
                        if (row.TriggerState != null)
                            row.TriggerState.text = value.boolValue ? "Set" : "Reset";
                        break;
                }
            }
        }

        private void UpdateModeLabel()
        {
            switch (GraphEditorModePolicy.GetParametersMode(Session.Mode))
            {
                case GraphEditorParametersMode.Defaults:
                    modeLabel.text = "Defaults";
                    modeLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                    break;
                case GraphEditorParametersMode.Live:
                    modeLabel.text = "Live";
                    modeLabel.style.color = new StyleColor(
                        Session.IsPreviewing ? GraphEditorStyles.PreviewColor : GraphEditorStyles.LiveColor);
                    break;
                case GraphEditorParametersMode.ReadOnlyDefaults:
                    modeLabel.text = "Read-only";
                    modeLabel.style.color = new StyleColor(GraphEditorStyles.WarningColor);
                    break;
                default:
                    modeLabel.text = string.Empty;
                    break;
            }

            addButton.SetEnabled(IsAuthoring && Session.HasDocument);
        }

        private void ShowAddMenu()
        {
            if (!IsAuthoring)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Float", false, () => Commands.Authoring.AddParameter(BlackboardParameterType.Float));
            menu.AddItem("Int", false, () => Commands.Authoring.AddParameter(BlackboardParameterType.Int));
            menu.AddItem("Bool", false, () => Commands.Authoring.AddParameter(BlackboardParameterType.Bool));
            menu.AddItem("Trigger", false, () => Commands.Authoring.AddParameter(BlackboardParameterType.Trigger));
            menu.DropDown(addButton.worldBound, addButton, DropdownMenuSizeMode.Content);
        }

        private static BlackboardParameterSnapshot FindParameter(
            GraphRuntimeSnapshot snapshot,
            string id,
            string runtimeName)
        {
            for (int i = 0; i < snapshot.parameters.Count; i++)
            {
                BlackboardParameterSnapshot parameter = snapshot.parameters[i];
                if (parameter != null && parameter.id == id)
                    return parameter;
            }

            if (!string.IsNullOrEmpty(runtimeName))
            {
                for (int i = 0; i < snapshot.parameters.Count; i++)
                {
                    BlackboardParameterSnapshot parameter = snapshot.parameters[i];
                    if (parameter != null && parameter.name == runtimeName)
                        return parameter;
                }
            }

            return null;
        }

        private static void SetValueIfNotFocused<T>(BaseField<T> field, T value)
        {
            if (field == null)
                return;

            if (field.focusController == null || field.focusController.focusedElement != field)
                field.SetValueWithoutNotify(value);
        }
    }
}
