using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Live (Play Mode) runtime discovery for one graph asset. Only runtimes with
    /// a matching non-empty GraphId are offered; preview runtimes are excluded.
    /// </summary>
    public sealed class LiveRuntimeSession : IDisposable
    {
        public sealed class Target
        {
            public GraphRuntime Runtime;
            public string Label;
            public StateMachineRunner Runner;
        }

        private readonly List<GraphRuntime> buffer = new List<GraphRuntime>();
        private readonly List<Target> targets = new List<Target>();

        private string graphId = string.Empty;
        private int selectedIndex = -1;
        private GraphRuntimeSnapshot snapshot;
        private bool disposed;

        public IReadOnlyList<Target> Targets => targets;
        public int SelectedIndex => selectedIndex;
        public GraphRuntimeSnapshot Snapshot => snapshot;

        public Target SelectedTarget =>
            selectedIndex >= 0 && selectedIndex < targets.Count ? targets[selectedIndex] : null;

        public GraphRuntime CurrentRuntime
        {
            get
            {
                Target target = SelectedTarget;
                return target != null && target.Runtime != null && !target.Runtime.IsDisposed
                    ? target.Runtime
                    : null;
            }
        }

        public bool IsPaused => CurrentRuntime != null && CurrentRuntime.IsPaused;

        public void SetGraphId(string value)
        {
            graphId = value ?? string.Empty;
            RefreshTargets();
        }

        public void RefreshTargets()
        {
            if (disposed)
                return;

            GraphRuntime previousRuntime = SelectedTarget?.Runtime;
            targets.Clear();
            buffer.Clear();

            if (string.IsNullOrEmpty(graphId))
            {
                selectedIndex = -1;
                snapshot = null;
                return;
            }

            GraphRuntimeRegistry.GetLiveRuntimes(buffer);

            for (int i = 0; i < buffer.Count; i++)
            {
                GraphRuntime runtime = buffer[i];
                if (runtime == null
                    || runtime.IsDisposed
                    || runtime.GraphId != graphId
                    || PreviewRuntimeTracker.IsPreview(runtime))
                {
                    continue;
                }

                if (ContainsRuntime(targets, runtime))
                    continue;

                StateMachineRunner runner = FindRunner(runtime);
                targets.Add(new Target
                {
                    Runtime = runtime,
                    Runner = runner,
                    Label = BuildLabel(runtime, runner, targets.Count),
                });
            }

            targets.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));

            int index = -1;
            if (previousRuntime != null)
                index = targets.FindIndex(target => ReferenceEquals(target.Runtime, previousRuntime));

            if (index < 0 && targets.Count > 0)
                index = 0;

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

            GraphRuntime runtime = CurrentRuntime;
            if (runtime == null)
            {
                snapshot = null;
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

        public void Dispose()
        {
            disposed = true;
            targets.Clear();
            buffer.Clear();
            snapshot = null;
            selectedIndex = -1;
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
            int index)
        {
            if (runner != null)
            {
                return "Runner: "
                    + (runner.gameObject != null ? runner.gameObject.name : "(null)");
            }

            if (!string.IsNullOrEmpty(runtime.Options.DebugName))
                return runtime.Options.DebugName;

            if (runtime.Options.Host != null)
                return runtime.Options.Host.name;

            return "Runtime " + (index + 1);
        }
    }
}
