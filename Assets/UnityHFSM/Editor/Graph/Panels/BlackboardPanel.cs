using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class BlackboardPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly ScrollView list;

        public BlackboardPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            style.width = 250f;
            style.borderRightWidth = 1f;
            style.borderRightColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            style.paddingLeft = 6f;
            style.paddingRight = 6f;
            style.paddingTop = 6f;

            var title = new Label("Blackboard");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            Add(title);

            list = new ScrollView();
            list.style.flexGrow = 1f;
            Add(list);

            var addButton = new Button(() =>
            {
                window.Controller?.AddParameter();
            })
            {
                text = "Add Parameter",
            };
            addButton.style.marginTop = 6f;
            Add(addButton);

            Refresh();
        }

        public void Refresh()
        {
            list.Clear();

            if (window.Controller == null || window.Controller.Model == null)
            {
                list.Add(new Label("No graph asset selected."));
                return;
            }

            GraphModel model = window.Controller.Model;
            if (model.parameters.Count == 0)
            {
                list.Add(new Label("No parameters. Add one to write conditions."));
                return;
            }

            for (int i = 0; i < model.parameters.Count; i++)
            {
                ParameterDef parameter = model.parameters[i];
                if (parameter != null)
                    list.Add(CreateRow(parameter));
            }
        }

        private VisualElement CreateRow(ParameterDef parameter)
        {
            var row = new VisualElement();
            row.style.marginBottom = 8f;
            row.style.paddingBottom = 6f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));

            var nameField = new TextField("Name")
            {
                value = parameter.name,
                isDelayed = true,
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                window.Controller?.RenameParameter(parameter.id, evt.newValue);
            });
            row.Add(nameField);

            var typeField = new EnumField("Type", parameter.type);
            typeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is BlackboardParameterType type)
                    window.Controller?.SetParameterType(parameter.id, type);
            });
            row.Add(typeField);

            switch (parameter.type)
            {
                case BlackboardParameterType.Float:
                {
                    var valueField = new FloatField("Default")
                    {
                        value = parameter.defaultFloat,
                        isDelayed = true,
                    };
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterFloatDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Int:
                {
                    var valueField = new IntegerField("Default")
                    {
                        value = parameter.defaultInt,
                        isDelayed = true,
                    };
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterIntDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }

                case BlackboardParameterType.Bool:
                case BlackboardParameterType.Trigger:
                {
                    var valueField = new Toggle("Default")
                    {
                        value = parameter.defaultBool,
                    };
                    valueField.RegisterValueChangedCallback(evt =>
                        window.Controller?.SetParameterBoolDefault(parameter.id, evt.newValue));
                    row.Add(valueField);
                    break;
                }
            }

            var deleteButton = new Button(() =>
            {
                window.Controller?.DeleteParameter(parameter.id);
            })
            {
                text = "Delete",
            };
            deleteButton.style.marginTop = 4f;
            row.Add(deleteButton);
            return row;
        }
    }
}
