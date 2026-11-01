using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Defaults in Author mode; live values in Preview/Live.</summary>
    public sealed class ParametersView : GraphEditorView
    {
        private const float TriggerSetWidth = 26f;
        private const float TriggerResetWidth = 36f;
        private const float TriggerStateWidth = 30f;

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
            nameField.style.minWidth = 0f;
            nameField.style.marginRight = GraphEditorMetrics.SmallSpacing;
            nameField.style.overflow = Overflow.Hidden;
            nameField.tooltip = parameter.name;
            nameField.SetEnabled(IsAuthoring);
            nameField.RegisterValueChangedCallback(evt =>
            {
                GraphCommandResult result = Commands.Authoring.RenameParameter(parameter.id, evt.newValue);
                if (!result.Success)
                {
                    nameField.SetValueWithoutNotify(parameter.name);
                    nameField.tooltip = parameter.name;
                }
                else
                {
                    nameField.tooltip = evt.newValue;
                }
            });
            row.Add(nameField);
            binding.NameField = nameField;

            var valueColumn = CreateValueColumn();

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var field = new FloatField
                    {
                        value = live && inRuntime ? runtime.GetFloat(runtimeName) : parameter.defaultFloat,
                        isDelayed = true,
                    };
                    ConfigureValueField(field, GraphEditorMetrics.ParametersValueColumnWidth);
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetFloat(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterFloatDefault(parameter.id, evt.newValue);
                    });
                    valueColumn.Add(field);
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
                    ConfigureValueField(field, GraphEditorMetrics.ParametersValueColumnWidth);
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetInt(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterIntDefault(parameter.id, evt.newValue);
                    });
                    valueColumn.Add(field);
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
                    field.style.minWidth = 20f;
                    field.style.maxWidth = 20f;
                    field.style.marginLeft = 0f;
                    field.style.marginRight = 0f;
                    field.style.marginTop = 0f;
                    field.style.marginBottom = 0f;
                    field.style.flexShrink = 0f;
                    field.SetEnabled(IsAuthoring || (live && inRuntime));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            Commands.Runtime.SetBool(runtimeName, evt.newValue);
                        else if (IsAuthoring)
                            Commands.Authoring.SetParameterBoolDefault(parameter.id, evt.newValue);
                    });
                    valueColumn.Add(field);
                    binding.BoolField = field;
                    break;
                }

                case BlackboardParameterType.Trigger:
                {
                    if (live && inRuntime)
                    {
                        var set = CreateCompactButton(
                            "Set",
                            "Set trigger",
                            TriggerSetWidth,
                            () => Commands.Runtime.SetTrigger(runtimeName));
                        set.style.marginRight = GraphEditorMetrics.TinySpacing;
                        valueColumn.Add(set);

                        var reset = CreateCompactButton(
                            "Reset",
                            "Reset trigger",
                            TriggerResetWidth,
                            () => Commands.Runtime.ResetTrigger(runtimeName));
                        reset.style.marginRight = GraphEditorMetrics.TinySpacing;
                        valueColumn.Add(reset);

                        var state = GraphEditorStyles.CreateMutedLabel("Reset");
                        state.style.width = TriggerStateWidth;
                        state.style.minWidth = TriggerStateWidth;
                        state.style.maxWidth = TriggerStateWidth;
                        state.style.fontSize = GraphEditorMetrics.SmallFontSize;
                        state.style.unityTextAlign = TextAnchor.MiddleCenter;
                        state.style.marginLeft = 0f;
                        state.style.marginRight = 0f;
                        state.style.flexShrink = 0f;
                        state.tooltip = "Trigger state: Reset";
                        valueColumn.Add(state);
                        binding.TriggerState = state;
                    }
                    else
                    {
                        var type = GraphEditorStyles.CreateMutedLabel("Trigger");
                        type.style.opacity = 0.7f;
                        type.style.flexShrink = 0f;
                        type.style.marginLeft = 0f;
                        type.style.marginRight = 0f;
                        valueColumn.Add(type);
                    }

                    break;
                }
            }

            row.Add(valueColumn);
            row.Add(CreateActionColumn(parameter));

            if (live)
            {
                row.tooltip = inRuntime
                    ? "Live value. Changes affect the running graph."
                    : "Not present in the running graph. Stop and restart to apply.";
            }
            else if (IsAuthoring)
            {
                row.tooltip = "Right-click or click ⋯ to delete.";
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Delete", _ => DeleteParameter(parameter));
                }));
            }

            rows.Add(binding);
            return row;
        }

        private static void ConfigureValueField(VisualElement field, float width)
        {
            field.style.width = width;
            field.style.minWidth = 0f;
            field.style.maxWidth = width;
            field.style.marginLeft = 0f;
            field.style.marginRight = 0f;
            field.style.marginTop = 0f;
            field.style.marginBottom = 0f;
            field.style.flexShrink = 0f;
        }

        private static VisualElement CreateValueColumn()
        {
            var column = new VisualElement();
            column.style.width = GraphEditorMetrics.ParametersValueColumnWidth;
            column.style.minWidth = GraphEditorMetrics.ParametersValueColumnWidth;
            column.style.maxWidth = GraphEditorMetrics.ParametersValueColumnWidth;
            column.style.flexDirection = FlexDirection.Row;
            column.style.alignItems = Align.Center;
            column.style.flexShrink = 0f;
            column.style.marginRight = GraphEditorMetrics.TinySpacing;
            return column;
        }

        private VisualElement CreateActionColumn(ParameterDef parameter)
        {
            var column = new VisualElement();
            column.style.width = GraphEditorMetrics.ParametersActionColumnWidth;
            column.style.minWidth = GraphEditorMetrics.ParametersActionColumnWidth;
            column.style.maxWidth = GraphEditorMetrics.ParametersActionColumnWidth;
            column.style.flexDirection = FlexDirection.Row;
            column.style.alignItems = Align.Center;
            column.style.flexShrink = 0f;

            if (!IsAuthoring)
                return column;

            var button = new Button { text = "⋯" };
            button.tooltip = "Delete parameter";
            button.style.width = GraphEditorMetrics.ParametersActionColumnWidth;
            button.style.minWidth = GraphEditorMetrics.ParametersActionColumnWidth;
            button.style.maxWidth = GraphEditorMetrics.ParametersActionColumnWidth;
            button.style.height = GraphEditorMetrics.SmallControlHeight;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.fontSize = GraphEditorMetrics.FontSize;
            button.style.flexShrink = 0f;

            float pointerDownTime = -1f;
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                pointerDownTime = Time.realtimeSinceStartup;
                ShowRowMenu(parameter, button);
                evt.StopPropagation();
            });
            button.clicked += () =>
            {
                if (Time.realtimeSinceStartup - pointerDownTime < 0.25f)
                    return;

                ShowRowMenu(parameter, button);
            };

            column.Add(button);
            return column;
        }

        private static Button CreateCompactButton(
            string text,
            string tooltip,
            float width,
            Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.tooltip = tooltip;
            button.style.width = width;
            button.style.minWidth = width;
            button.style.maxWidth = width;
            button.style.height = GraphEditorMetrics.SmallControlHeight;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = GraphEditorMetrics.TinySpacing;
            button.style.paddingRight = GraphEditorMetrics.TinySpacing;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.fontSize = GraphEditorMetrics.SmallFontSize;
            button.style.flexShrink = 0f;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.overflow = Overflow.Hidden;
            button.style.textOverflow = TextOverflow.Ellipsis;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }

        private void ShowRowMenu(ParameterDef parameter, VisualElement anchor)
        {
            if (!IsAuthoring || Commands == null || parameter == null || anchor == null)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Delete", false, () => DeleteParameter(parameter));
            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Content);
        }

        private void DeleteParameter(ParameterDef parameter)
        {
            if (Commands == null || parameter == null)
                return;

            Commands.Authoring.DeleteParameter(parameter.id);
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
                        {
                            string stateText = value.boolValue ? "Set" : "Reset";
                            row.TriggerState.text = stateText;
                            row.TriggerState.tooltip = "Trigger state: " + stateText;
                        }
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
