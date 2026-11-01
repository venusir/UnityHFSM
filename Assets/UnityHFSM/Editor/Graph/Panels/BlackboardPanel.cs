using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class BlackboardPanel : VisualElement
    {
        private sealed class ParameterRow
        {
            public ParameterDef definition;
            public string runtimeName;
            public BlackboardParameterType type;
            public TextField nameField;
            public FloatField floatField;
            public IntegerField intField;
            public Toggle boolField;
            public Label triggerState;
        }

        private readonly StateMachineGraphWindow window;
        private readonly ScrollView list;
        private readonly Button addButton;
        private readonly Label liveStatus;
        private readonly List<ParameterRow> rows = new List<ParameterRow>();

        public BlackboardPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            GraphEditorStyles.SetBorder(this, GraphEditorStyles.SeparatorColor, rightWidth: 1f);

            var header = GraphEditorStyles.CreateHeaderBand("Parameters", out VisualElement headerRight);
            Add(header);

            liveStatus = GraphEditorStyles.CreateMutedLabel();
            liveStatus.style.marginRight = 6f;
            liveStatus.style.display = DisplayStyle.None;
            headerRight.Add(liveStatus);

            addButton = new Button(ShowAddMenu) { text = "+" };
            addButton.tooltip = "Add parameter";
            addButton.style.width = 22f;
            addButton.style.height = 18f;
            addButton.style.marginLeft = 0f;
            addButton.style.marginRight = 0f;
            addButton.style.marginTop = 0f;
            addButton.style.marginBottom = 0f;
            headerRight.Add(addButton);

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 6f;
            body.style.paddingRight = 6f;
            body.style.paddingTop = 6f;
            Add(body);

            list = new ScrollView();
            list.style.flexGrow = 1f;
            body.Add(list);

            Refresh();
        }

        public void Refresh()
        {
            list.Clear();
            rows.Clear();

            bool hasController = window.Controller != null && window.Controller.Model != null;
            GraphRuntime runtime = window.CurrentRuntime;
            bool live = runtime != null && !runtime.IsDisposed;

            addButton.SetEnabled(hasController && !live);
            addButton.tooltip = live ? "Stop the preview to edit parameters." : "Add parameter";

            if (live)
            {
                liveStatus.text = window.IsPreviewing ? "Preview" : "Live";
                liveStatus.tooltip = "Values are read from the running graph.";
                liveStatus.style.color = new StyleColor(
                    window.IsPreviewing ? GraphEditorStyles.PreviewColor : GraphEditorStyles.LiveColor);
                liveStatus.style.display = DisplayStyle.Flex;
            }
            else
            {
                liveStatus.style.display = DisplayStyle.None;
            }

            if (!hasController)
            {
                list.Add(new Label("No graph asset selected."));
                return;
            }

            GraphModel model = window.Controller.Model;
            if (model.parameters.Count == 0)
            {
                list.Add(new Label("No parameters. Use + to add one."));
                return;
            }

            for (int i = 0; i < model.parameters.Count; i++)
            {
                ParameterDef parameter = model.parameters[i];
                if (parameter != null)
                    list.Add(CreateRow(parameter, runtime, live));
            }
        }

        public void UpdateLive(GraphRuntimeSnapshot snapshot)
        {
            if (snapshot == null || rows.Count == 0)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                ParameterRow row = rows[i];
                BlackboardParameterSnapshot value = FindParameter(snapshot, row.definition.id, row.runtimeName);
                if (value == null || value.type != row.type)
                    continue;

                switch (row.type)
                {
                    case BlackboardParameterType.Float:
                        SetValueIfNotFocused(row.floatField, value.floatValue);
                        break;
                    case BlackboardParameterType.Int:
                        SetValueIfNotFocused(row.intField, value.intValue);
                        break;
                    case BlackboardParameterType.Bool:
                        SetValueIfNotFocused(row.boolField, value.boolValue);
                        break;
                    case BlackboardParameterType.Trigger:
                        if (row.triggerState != null)
                            row.triggerState.text = value.boolValue ? "Set" : "Reset";
                        break;
                }
            }
        }

        private void ShowAddMenu()
        {
            if (window.Controller == null || window.Controller.Model == null || window.CurrentRuntime != null)
                return;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Float", false, () => AddParameter(BlackboardParameterType.Float));
            menu.AddItem("Int", false, () => AddParameter(BlackboardParameterType.Int));
            menu.AddItem("Bool", false, () => AddParameter(BlackboardParameterType.Bool));
            menu.AddItem("Trigger", false, () => AddParameter(BlackboardParameterType.Trigger));
            menu.DropDown(addButton.worldBound, addButton, DropdownMenuSizeMode.Content);
        }

        private void AddParameter(BlackboardParameterType type)
        {
            window.Controller?.AddParameter(type);
        }

        private VisualElement CreateRow(ParameterDef parameter, GraphRuntime runtime, bool live)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22f;
            row.style.paddingLeft = 2f;
            row.style.paddingRight = 2f;
            GraphEditorStyles.SetBorder(row, GraphEditorStyles.SeparatorColor, bottomWidth: 1f);
            row.tooltip = live ? "Live values are written to the running graph." : "Right-click to delete.";

            bool inRuntime = false;
            string runtimeName = null;

            if (live && runtime != null && runtime.Schema.TryGetIndexById(parameter.id, out int runtimeIndex))
            {
                BlackboardParameterInfo info = runtime.Schema.GetParameter(runtimeIndex);
                if (info != null && info.Type == parameter.type)
                {
                    inRuntime = true;
                    runtimeName = info.Name;
                }
            }

            var binding = new ParameterRow
            {
                definition = parameter,
                runtimeName = runtimeName,
                type = parameter.type,
            };

            var nameField = new TextField
            {
                value = parameter.name,
                isDelayed = true,
            };
            nameField.style.flexGrow = 1f;
            nameField.style.flexShrink = 1f;
            nameField.style.marginRight = 4f;
            nameField.SetEnabled(!live);
            nameField.tooltip = live ? "Stop the preview to rename parameters." : string.Empty;
            nameField.RegisterValueChangedCallback(evt =>
                window.Controller?.RenameParameter(parameter.id, evt.newValue));
            row.Add(nameField);
            binding.nameField = nameField;

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var valueField = new FloatField
                    {
                        value = inRuntime ? runtime.GetFloat(runtimeName) : parameter.defaultFloat,
                        isDelayed = true,
                    };
                    valueField.style.width = 64f;
                    valueField.SetEnabled(!live || inRuntime);
                    valueField.tooltip = live && !inRuntime
                        ? "This parameter is not in the running graph. Restart the preview."
                        : string.Empty;
                    valueField.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            runtime.SetFloat(runtimeName, evt.newValue);
                        else if (!live)
                            window.Controller?.SetParameterFloatDefault(parameter.id, evt.newValue);
                    });
                    row.Add(valueField);
                    binding.floatField = valueField;
                    break;
                }

                case BlackboardParameterType.Int:
                {
                    var valueField = new IntegerField
                    {
                        value = inRuntime ? runtime.GetInt(runtimeName) : parameter.defaultInt,
                        isDelayed = true,
                    };
                    valueField.style.width = 64f;
                    valueField.SetEnabled(!live || inRuntime);
                    valueField.tooltip = live && !inRuntime
                        ? "This parameter is not in the running graph. Restart the preview."
                        : string.Empty;
                    valueField.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            runtime.SetInt(runtimeName, evt.newValue);
                        else if (!live)
                            window.Controller?.SetParameterIntDefault(parameter.id, evt.newValue);
                    });
                    row.Add(valueField);
                    binding.intField = valueField;
                    break;
                }

                case BlackboardParameterType.Bool:
                {
                    var valueField = new Toggle
                    {
                        value = inRuntime ? runtime.GetBool(runtimeName) : parameter.defaultBool,
                    };
                    valueField.style.width = 20f;
                    valueField.SetEnabled(!live || inRuntime);
                    valueField.tooltip = live && !inRuntime
                        ? "This parameter is not in the running graph. Restart the preview."
                        : string.Empty;
                    valueField.RegisterValueChangedCallback(evt =>
                    {
                        if (live && inRuntime)
                            runtime.SetBool(runtimeName, evt.newValue);
                        else if (!live)
                            window.Controller?.SetParameterBoolDefault(parameter.id, evt.newValue);
                    });
                    row.Add(valueField);
                    binding.boolField = valueField;
                    break;
                }

                case BlackboardParameterType.Trigger:
                {
                    if (live && inRuntime)
                    {
                        var setButton = new Button(() => runtime.SetTrigger(runtimeName))
                        {
                            text = "Set",
                        };
                        setButton.style.height = 18f;
                        setButton.style.marginLeft = 0f;
                        setButton.style.marginRight = 2f;
                        setButton.style.marginTop = 0f;
                        setButton.style.marginBottom = 0f;
                        row.Add(setButton);

                        var resetButton = new Button(() => runtime.ResetTrigger(runtimeName))
                        {
                            text = "Reset",
                        };
                        resetButton.style.height = 18f;
                        resetButton.style.marginLeft = 0f;
                        resetButton.style.marginRight = 2f;
                        resetButton.style.marginTop = 0f;
                        resetButton.style.marginBottom = 0f;
                        row.Add(resetButton);

                        var stateLabel = new Label("Reset");
                        stateLabel.style.width = 38f;
                        stateLabel.style.fontSize = 10f;
                        stateLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                        row.Add(stateLabel);
                        binding.triggerState = stateLabel;
                    }
                    else
                    {
                        var typeLabel = new Label("Trigger");
                        typeLabel.style.opacity = 0.6f;
                        typeLabel.style.fontSize = 10f;
                        typeLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                        row.Add(typeLabel);
                    }

                    break;
                }
            }

            if (live)
                row.tooltip = inRuntime
                    ? "Live value. Changes affect the running graph."
                    : "Not present in the running graph. Restart the preview.";
            else
            {
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Delete", _ =>
                        window.Controller?.DeleteParameter(parameter.id));
                }));
            }

            rows.Add(binding);
            return row;
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
