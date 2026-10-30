using System.Collections.Generic;
using System.Text;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Runtime debugging panel. It polls a snapshot at a low frequency and keeps
    /// field bindings so editing does not rebuild the UI every tick.
    /// </summary>
    public sealed class RuntimePanel : VisualElement
    {
        private sealed class ParameterBinding
        {
            public string name;
            public BlackboardParameterType type;
            public FloatField floatField;
            public IntegerField intField;
            public Toggle boolField;
            public Label triggerLabel;
        }

        private readonly StateMachineGraphWindow window;
        private readonly DropdownField runnerField;
        private readonly Label statusLabel;
        private readonly Label pathLabel;
        private readonly ScrollView blackboardList;
        private readonly Label logLabel;
        private readonly Button pauseButton;

        private readonly List<StateMachineRunner> runners = new List<StateMachineRunner>();
        private readonly List<ParameterBinding> bindings = new List<ParameterBinding>();

        private StateMachineRunner boundRunner;
        private bool updatingRunnerField;

        public GraphRuntimeSnapshot CurrentSnapshot { get; private set; }
        public StateMachineRunner BoundRunner => boundRunner;

        public RuntimePanel(StateMachineGraphWindow window)
        {
            this.window = window;

            Add(GraphEditorStyles.CreateHeaderBand("Runtime", out _));

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 8f;
            body.style.paddingRight = 8f;
            body.style.paddingTop = 4f;
            Add(body);

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            body.Add(toolbar);

            runnerField = new DropdownField("Runner");
            runnerField.style.minWidth = 220f;
            runnerField.RegisterValueChangedCallback(evt =>
            {
                if (updatingRunnerField)
                    return;

                int index = runnerField.choices.IndexOf(evt.newValue);
                if (index >= 0 && index < runners.Count)
                    SetBoundRunner(runners[index]);
            });
            toolbar.Add(runnerField);

            var refreshButton = new Button(RefreshRunnerList) { text = "Refresh Runners" };
            toolbar.Add(refreshButton);

            pauseButton = new Button(TogglePause) { text = "Pause" };
            toolbar.Add(pauseButton);

            var stepButton = new Button(Step) { text = "Step" };
            toolbar.Add(stepButton);

            var rebuildButton = new Button(Rebuild) { text = "Rebuild" };
            toolbar.Add(rebuildButton);

            statusLabel = new Label("No runner bound.");
            body.Add(statusLabel);

            pathLabel = new Label(string.Empty);
            pathLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(pathLabel);

            blackboardList = new ScrollView();
            blackboardList.style.maxHeight = 110f;
            body.Add(blackboardList);

            var logScroll = new ScrollView();
            logScroll.style.flexGrow = 1f;
            logLabel = new Label(string.Empty);
            logLabel.style.whiteSpace = WhiteSpace.Normal;
            logScroll.Add(logLabel);
            body.Add(logScroll);

            RefreshRunnerList();
        }

        public void RefreshRunnerList()
        {
            var found = new List<StateMachineRunner>();

            IReadOnlyList<StateMachineRunner> registered = StateMachineRunnerRegistry.Runners;
            for (int i = 0; i < registered.Count; i++)
                AddUnique(found, registered[i]);

            StateMachineRunner[] sceneRunners = Object.FindObjectsOfType<StateMachineRunner>();
            for (int i = 0; i < sceneRunners.Length; i++)
                AddUnique(found, sceneRunners[i]);

            runners.Clear();
            runners.AddRange(found);

            var names = new List<string>();
            for (int i = 0; i < runners.Count; i++)
                names.Add(GetRunnerName(runners[i]));

            updatingRunnerField = true;
            runnerField.choices = names;

            if (runners.Count == 0)
            {
                boundRunner = null;
                runnerField.SetValueWithoutNotify(string.Empty);
                statusLabel.text = "No StateMachineRunner in the scene. Enter Play Mode with a runner to debug.";
                pathLabel.text = string.Empty;
                logLabel.text = string.Empty;
                blackboardList.Clear();
                bindings.Clear();
                CurrentSnapshot = null;
                updatingRunnerField = false;
                return;
            }

            int selected = runners.IndexOf(boundRunner);
            if (selected < 0)
            {
                selected = 0;
                for (int i = 0; i < runners.Count; i++)
                {
                    if (runners[i].Graph == window.Asset)
                    {
                        selected = i;
                        break;
                    }
                }
            }

            runnerField.SetValueWithoutNotify(names[selected]);
            updatingRunnerField = false;
            SetBoundRunner(runners[selected]);
        }

        public void BindRunner(StateMachineRunner runner)
        {
            if (runner == null)
                return;

            if (!runners.Contains(runner))
                RefreshRunnerList();

            int index = runners.IndexOf(runner);
            if (index < 0 || index >= runnerField.choices.Count)
                return;

            updatingRunnerField = true;
            runnerField.SetValueWithoutNotify(runnerField.choices[index]);
            updatingRunnerField = false;
            SetBoundRunner(runner);
        }

        public void UpdateLive()
        {
            if (boundRunner == null)
            {
                CurrentSnapshot = null;
                return;
            }

            if (boundRunner.Runtime == null)
            {
                CurrentSnapshot = null;
                statusLabel.text = "Runner has no runtime. Enter Play Mode to start debugging.";
                return;
            }

            CurrentSnapshot = boundRunner.Runtime.GetSnapshot();
            UpdateSnapshot(CurrentSnapshot);
        }

        private void SetBoundRunner(StateMachineRunner runner)
        {
            boundRunner = runner;
            BuildBlackboardBindings();
            UpdateLive();
        }

        private void BuildBlackboardBindings()
        {
            bindings.Clear();
            blackboardList.Clear();

            if (boundRunner == null || boundRunner.Runtime == null)
            {
                blackboardList.Add(new Label("Runtime blackboard is not available."));
                return;
            }

            BlackboardSchema schema = boundRunner.Runtime.Schema;
            for (int i = 0; i < schema.Parameters.Count; i++)
            {
                BlackboardParameterInfo parameter = schema.Parameters[i];
                if (parameter == null)
                    continue;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var label = new Label(parameter.Name);
                label.style.width = 110f;
                row.Add(label);

                var binding = new ParameterBinding
                {
                    name = parameter.Name,
                    type = parameter.Type,
                };

                switch (parameter.Type)
                {
                    case BlackboardParameterType.Float:
                    {
                        var field = new FloatField { isDelayed = true };
                        field.style.flexGrow = 1f;
                        field.RegisterValueChangedCallback(evt =>
                            boundRunner?.SetFloat(parameter.Name, evt.newValue));
                        row.Add(field);
                        binding.floatField = field;
                        break;
                    }

                    case BlackboardParameterType.Int:
                    {
                        var field = new IntegerField { isDelayed = true };
                        field.style.flexGrow = 1f;
                        field.RegisterValueChangedCallback(evt =>
                            boundRunner?.SetInt(parameter.Name, evt.newValue));
                        row.Add(field);
                        binding.intField = field;
                        break;
                    }

                    case BlackboardParameterType.Bool:
                    {
                        var field = new Toggle();
                        field.RegisterValueChangedCallback(evt =>
                            boundRunner?.SetBool(parameter.Name, evt.newValue));
                        row.Add(field);
                        binding.boolField = field;
                        break;
                    }

                    case BlackboardParameterType.Trigger:
                    {
                        var setButton = new Button(() => boundRunner?.SetTrigger(parameter.Name))
                        {
                            text = "Set",
                        };
                        row.Add(setButton);

                        var resetButton = new Button(() => boundRunner?.ResetTrigger(parameter.Name))
                        {
                            text = "Reset",
                        };
                        row.Add(resetButton);

                        var triggerLabel = new Label("Reset");
                        triggerLabel.style.width = 50f;
                        row.Add(triggerLabel);
                        binding.triggerLabel = triggerLabel;
                        break;
                    }
                }

                bindings.Add(binding);
                blackboardList.Add(row);
            }
        }

        private void UpdateSnapshot(GraphRuntimeSnapshot snapshot)
        {
            statusLabel.text =
                (snapshot.isStarted ? "Started" : "Not started")
                + (snapshot.isPaused ? " / Paused" : string.Empty)
                + " / t=" + snapshot.time.ToString("0.00");

            pathLabel.text = "Active: " + BuildActivePath(snapshot);

            for (int i = 0; i < bindings.Count; i++)
            {
                ParameterBinding binding = bindings[i];
                BlackboardParameterSnapshot value = FindParameter(snapshot, binding.name);
                if (value == null)
                    continue;

                switch (binding.type)
                {
                    case BlackboardParameterType.Float:
                        SetValueIfNotFocused(binding.floatField, value.floatValue);
                        break;
                    case BlackboardParameterType.Int:
                        SetValueIfNotFocused(binding.intField, value.intValue);
                        break;
                    case BlackboardParameterType.Bool:
                        SetValueIfNotFocused(binding.boolField, value.boolValue);
                        break;
                    case BlackboardParameterType.Trigger:
                        if (binding.triggerLabel != null)
                            binding.triggerLabel.text = value.boolValue ? "Set" : "Reset";
                        break;
                }
            }

            pauseButton.text = snapshot.isPaused ? "Resume" : "Pause";
            logLabel.text = BuildTransitionLog(snapshot.transitions);
        }

        private void TogglePause()
        {
            if (boundRunner == null)
                return;

            if (boundRunner.IsPaused)
                boundRunner.Resume();
            else
                boundRunner.Pause();

            UpdateLive();
        }

        private void Step()
        {
            if (boundRunner == null)
                return;

            boundRunner.Step(Time.deltaTime);
            UpdateLive();
        }

        private void Rebuild()
        {
            if (boundRunner == null)
                return;

            boundRunner.Rebuild();
            BuildBlackboardBindings();
            UpdateLive();
        }

        private string BuildActivePath(GraphRuntimeSnapshot snapshot)
        {
            if (snapshot.activePathStateIds.Count == 0)
                return "(none)";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.activePathStateIds.Count; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                StateDef state = window.Controller != null
                    ? window.Controller.FindState(snapshot.activePathStateIds[i])
                    : null;

                builder.Append(state != null ? state.name : snapshot.activePathStateIds[i]);
            }

            return builder.ToString();
        }

        private static string BuildTransitionLog(List<TransitionRecord> transitions)
        {
            if (transitions == null || transitions.Count == 0)
                return "Transition log: (empty)";

            var builder = new StringBuilder("Transition log:");
            int start = Mathf.Max(0, transitions.Count - 20);

            for (int i = start; i < transitions.Count; i++)
            {
                TransitionRecord record = transitions[i];
                builder.Append('\n');
                builder.Append('#');
                builder.Append(record.sequence);
                builder.Append(" t=");
                builder.Append(record.time.ToString("0.00"));
                builder.Append(' ');
                builder.Append(record.machineName);
                builder.Append(": ");
                builder.Append(string.IsNullOrEmpty(record.fromStateName) ? "(Any)" : record.fromStateName);
                builder.Append(" -> ");
                builder.Append(record.isExit
                    ? "Exit"
                    : string.IsNullOrEmpty(record.toStateName) ? "(none)" : record.toStateName);
            }

            return builder.ToString();
        }

        private static BlackboardParameterSnapshot FindParameter(GraphRuntimeSnapshot snapshot, string name)
        {
            for (int i = 0; i < snapshot.parameters.Count; i++)
            {
                if (snapshot.parameters[i].name == name)
                    return snapshot.parameters[i];
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

        private static void AddUnique(List<StateMachineRunner> list, StateMachineRunner runner)
        {
            if (runner != null && !list.Contains(runner))
                list.Add(runner);
        }

        private static string GetRunnerName(StateMachineRunner runner)
        {
            if (runner == null)
                return "(null)";

            string graphName = runner.Graph != null ? runner.Graph.name : "no graph";
            return runner.gameObject.name + " [" + graphName + "]";
        }
    }
}
