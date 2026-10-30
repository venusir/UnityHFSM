using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class BlackboardPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly ScrollView list;
        private readonly Button addButton;

        public BlackboardPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            style.width = 250f;
            GraphEditorStyles.SetBorder(this, GraphEditorStyles.SeparatorColor, rightWidth: 1f);

            var header = GraphEditorStyles.CreateHeaderBand("Parameters", out VisualElement headerRight);
            Add(header);

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

            bool hasController = window.Controller != null && window.Controller.Model != null;
            addButton.SetEnabled(hasController);

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
                    list.Add(CreateRow(parameter));
            }
        }

        private void ShowAddMenu()
        {
            if (window.Controller == null || window.Controller.Model == null)
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

        private VisualElement CreateRow(ParameterDef parameter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22f;
            row.style.paddingLeft = 2f;
            row.style.paddingRight = 2f;
            GraphEditorStyles.SetBorder(row, GraphEditorStyles.SeparatorColor, bottomWidth: 1f);
            row.tooltip = "Right-click to delete.";

            var nameField = new TextField
            {
                value = parameter.name,
                isDelayed = true,
            };
            nameField.style.flexGrow = 1f;
            nameField.style.flexShrink = 1f;
            nameField.style.marginRight = 4f;
            nameField.RegisterValueChangedCallback(evt =>
                window.Controller?.RenameParameter(parameter.id, evt.newValue));
            row.Add(nameField);

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var valueField = new FloatField
                    {
                        value = parameter.defaultFloat,
                        isDelayed = true,
                    };
                    valueField.style.width = 64f;
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterFloatDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Int:
                {
                    var valueField = new IntegerField
                    {
                        value = parameter.defaultInt,
                        isDelayed = true,
                    };
                    valueField.style.width = 64f;
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterIntDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Bool:
                {
                    var valueField = new Toggle
                    {
                        value = parameter.defaultBool,
                    };
                    valueField.style.width = 20f;
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterBoolDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Trigger:
                {
                    var typeLabel = new Label("Trigger");
                    typeLabel.style.opacity = 0.6f;
                    typeLabel.style.fontSize = 10f;
                    typeLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    row.Add(typeLabel);
                    break;
                }
            }

            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Delete", _ =>
                    window.Controller?.DeleteParameter(parameter.id));
            }));

            return row;
        }
    }
}
