using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Marks edit-mode preview runtimes so other windows do not mistake them for
    /// live game runtimes. Uses weak references and is reset when Play Mode starts.
    /// </summary>
    public static class PreviewRuntimeTracker
    {
        private static readonly object sync = new object();
        private static readonly List<WeakReference<GraphRuntime>> runtimes =
            new List<WeakReference<GraphRuntime>>();

        public static void Track(GraphRuntime runtime)
        {
            if (runtime == null)
                return;

            lock (sync)
            {
                for (int i = 0; i < runtimes.Count; i++)
                {
                    if (runtimes[i].TryGetTarget(out GraphRuntime existing)
                        && ReferenceEquals(existing, runtime))
                    {
                        return;
                    }
                }

                runtimes.Add(new WeakReference<GraphRuntime>(runtime));
            }
        }

        public static void Untrack(GraphRuntime runtime)
        {
            if (runtime == null)
                return;

            lock (sync)
            {
                for (int i = runtimes.Count - 1; i >= 0; i--)
                {
                    if (!runtimes[i].TryGetTarget(out GraphRuntime existing)
                        || existing == null
                        || ReferenceEquals(existing, runtime))
                    {
                        runtimes.RemoveAt(i);
                    }
                }
            }
        }

        public static bool IsPreview(GraphRuntime runtime)
        {
            if (runtime == null)
                return false;

            lock (sync)
            {
                for (int i = runtimes.Count - 1; i >= 0; i--)
                {
                    if (!runtimes[i].TryGetTarget(out GraphRuntime existing) || existing == null)
                    {
                        runtimes.RemoveAt(i);
                        continue;
                    }

                    if (ReferenceEquals(existing, runtime))
                        return true;
                }
            }

            return false;
        }

        public static void Reset()
        {
            lock (sync)
            {
                runtimes.Clear();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            Reset();
        }
    }
}
