using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Unifies the edit-mode preview and live runtimes discovered in Play Mode.
    /// It owns target selection and snapshot polling; it never ticks live
    /// runtimes, because their owner code does that.
    /// </summary>
    public sealed class RuntimeSession : IDisposable
    {
        public sealed class Target
        {
            public GraphRuntime Runtime;
            public string Label;
            public StateMachineRunner Runner;
            public bool MatchesGraph;
        }

        private readonly GraphPreviewSession preview;
        private readonly List<GraphRuntime> runtimeBuffer = new List<GraphRuntime>();
        private readonly List<Target> targets = new List<Target>();

        private string graphId = string.Empty;
        private int selectedIndex = -1;
        private GraphRuntimeSnapshot snapshot;
        private bool disposed;

        public RuntimeSession(GraphPreviewSession preview)
        {
            this.preview = preview;
        }

        public GraphPreviewSession Preview => preview;
        public IReadOnlyList<Target> Targets => targets;
        public int SelectedIndex => selectedIndex;
        public GraphRuntimeSnapshot Snapshot => snapshot;
        public bool IsPreviewing => preview != null && preview.IsRunning;
        public bool IsPaused => IsPreviewing
            ? preview.IsPaused
            : CurrentRuntime != null && CurrentRuntime.IsPaused;

        public Target SelectedTarget =>
            selectedIndex >= 0 && selectedIndex < targets.Count ? targets[selectedIndex] : null;

        public GraphRuntime CurrentRuntime
        {
            get
            {
                if (IsPreviewing)
                    return preview.Runtime;

                Target target = SelectedTarget;
                return target != null && target.Runtime != null && !target.Runtime.IsDisposed
                    ? target.Runtime
                    : null;
            }
        }

        public bool CanRestart =>
            IsPreviewing || (SelectedTarget != null && SelectedTarget.Runner != null);

        public void SetGraphId(string value)
        {
            graphId = value ?? string.Empty;
        }

        public void RefreshLiveTargets()
        {
            if (disposed)
                return;

            GraphRuntime previousRuntime = SelectedTarget?.Runtime;
            string previousLabel = SelectedTarget?.Label;

            targets.Clear();
            runtimeBuffer.Clear();
            GraphRuntimeRegistry.GetLiveRuntimes(runtimeBuffer);

            for (int i = 0; i < runtimeBuffer.Count; i++)
            {
                GraphRuntime runtime = runtimeBuffer[i];
                if (runtime == null || runtime.IsDisposed)
                    continue;

                if (preview != null && ReferenceEquals(runtime, preview.Runtime))
                    continue;

                StateMachineRunner runner = FindRunner(runtime);
                bool matches = !string.IsNullOrEmpty(graphId) && runtime.GraphId == graphId;

                var target = new Target
                {
                    Runtime = runtime,
                    Runner = runner,
                    MatchesGraph = matches,
                    Label = BuildLabel(runtime, runner, i, matches),
                };

                if (!ContainsRuntime(targets, runtime))
                    targets.Add(target);
            }

            targets.Sort((a, b) =>
            {
                if (a.MatchesGraph != b.MatchesGraph)
                    return a.MatchesGraph ? -1 : 1;

                return string.CompareOrdinal(a.Label, b.Label);
            });

            int index = -1;
            if (previousRuntime != null)
                index = targets.FindIndex(target => ReferenceEquals(target.Runtime, previousRuntime));

            if (index < 0 && !string.IsNullOrEmpty(previousLabel))
                index = targets.FindIndex(target => target.Label == previousLabel);

            if (index < 0)
                index = targets.FindIndex(target => target.MatchesGraph);

            // Do not auto-select a runtime for another graph; the user can pick
            // it explicitly from the target dropdown.
            selectedIndex = index;
        }

        public void SelectTarget(int index)
        {
            if (index < 0 || index >= targets.Count)
                return;

            selectedIndex = index;
            snapshot = null;
        }

        public void Poll()
        {
            if (disposed)
                return;

            if (IsPreviewing)
            {
                snapshot = preview.GetSnapshot();
                return;
            }

            Target target = SelectedTarget;
            GraphRuntime runtime = target != null ? target.Runtime : null;

            if (runtime == null || runtime.IsDisposed)
            {
                snapshot = null;
                RefreshLiveTargets();
                return;
            }

            try
            {
                snapshot = runtime.GetSnapshot();
            }
            catch (Exception)
            {
                snapshot = null;
            }
        }

        public void TogglePause()
        {
            if (IsPreviewing)
            {
                preview.TogglePause();
                return;
            }

            GraphRuntime runtime = CurrentRuntime;
            if (runtime == null)
                return;

            if (runtime.IsPaused)
                runtime.Resume();
            else
                runtime.Pause();
        }

        public void Step()
        {
            if (IsPreviewing)
            {
                preview.Step();
                return;
            }

            CurrentRuntime?.Step(1f / 60f);
        }

        public void Restart()
        {
            if (IsPreviewing || (preview != null && preview.Runtime != null))
            {
                preview.Restart(true);
                return;
            }

            Target target = SelectedTarget;
            target?.Runner?.Rebuild();
        }

        public void Dispose()
        {
            disposed = true;
            targets.Clear();
            runtimeBuffer.Clear();
            snapshot = null;
        }

        private static bool ContainsRuntime(List<Target> list, GraphRuntime runtime)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i].Runtime, runtime))
                    return true;
            }

            return false;
        }

        private static StateMachineRunner FindRunner(GraphRuntime runtime)
        {
            IReadOnlyList<StateMachineRunner> runners = StateMachineRunnerRegistry.Runners;
            for (int i = 0; i < runners.Count; i++)
            {
                StateMachineRunner runner = runners[i];
                if (runner != null && ReferenceEquals(runner.Runtime, runtime))
                    return runner;
            }

            return null;
        }

        private static string BuildLabel(
            GraphRuntime runtime,
            StateMachineRunner runner,
            int index,
            bool matches)
        {
            string label;

            if (runner != null)
            {
                label = "Runner: " + (runner.gameObject != null ? runner.gameObject.name : "(null)");
            }
            else if (!string.IsNullOrEmpty(runtime.Options.DebugName))
            {
                label = runtime.Options.DebugName;
            }
            else if (runtime.Options.Host != null)
            {
                label = runtime.Options.Host.name;
            }
            else
            {
                label = "Runtime " + (index + 1);
            }

            return matches ? label : label + " · other graph";
        }
    }
}
