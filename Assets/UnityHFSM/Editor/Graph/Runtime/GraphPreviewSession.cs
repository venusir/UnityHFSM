using System;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Edit-mode owner of a GraphRuntime built from an asset model. It is driven
    /// by the window's EditorApplication.update hook and needs no scene object.
    /// </summary>
    public sealed class GraphPreviewSession : IDisposable
    {
        private readonly GraphModel model;

        private GraphRuntime runtime;
        private double lastTime = -1d;
        private float speed = 1f;
        private GameObject host;
        private bool isRunning;
        private bool isPaused;
        private string lastError;
        private GraphValidationResult lastValidation;
        private bool disposed;

        public GraphPreviewSession(GraphModel model, GameObject host = null, float speed = 1f)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.host = host;
            Speed = speed;
        }

        public event Action Changed;

        public GraphRuntime Runtime => runtime;
        public bool IsRunning => isRunning;
        public bool IsPaused => isPaused;
        public string LastError => lastError;
        public GraphValidationResult LastValidation => lastValidation;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Clamp(value, 0.1f, 4f);
        }

        public GameObject Host
        {
            get => host;
            set
            {
                if (host == value)
                    return;

                host = value;

                if (isRunning)
                    Restart(true);
            }
        }

        public bool Start()
        {
            if (disposed || isRunning)
                return isRunning;

            ClearError();

            GraphRuntime next;
            try
            {
                next = CreateRuntime();
            }
            catch (GraphBuildException exception)
            {
                lastValidation = exception.Validation;
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }

            try
            {
                next.Start();
            }
            catch (Exception exception)
            {
                SafeDispose(next);
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }

            runtime = next;
            PreviewRuntimeTracker.Track(next);
            isRunning = true;
            isPaused = false;
            lastTime = -1d;
            NotifyChanged();
            return true;
        }

        public void Stop()
        {
            GraphRuntime previous = runtime;
            if (previous == null && !isRunning)
                return;

            runtime = null;
            isRunning = false;
            isPaused = false;
            lastTime = -1d;
            lastError = null;
            lastValidation = null;

            if (previous != null)
            {
                PreviewRuntimeTracker.Untrack(previous);

                try
                {
                    previous.Dispose();
                }
                catch (Exception exception)
                {
                    lastError = exception.Message;
                }
            }

            NotifyChanged();
        }

        /// <summary>
        /// Rebuilds the runtime from the current model. On failure the existing
        /// runtime is kept so debugging is not lost.
        /// </summary>
        public bool Restart(bool preserveBlackboard = true)
        {
            if (disposed)
                return false;

            GraphRuntime previous = runtime;
            GraphRuntime next;

            try
            {
                next = CreateRuntime();
            }
            catch (GraphBuildException exception)
            {
                lastValidation = exception.Validation;
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }

            if (preserveBlackboard && previous != null)
                next.Blackboard.CopyValuesFrom(previous.Blackboard);

            try
            {
                next.Start();
            }
            catch (Exception exception)
            {
                SafeDispose(next);
                lastError = exception.Message;
                NotifyChanged();
                return false;
            }

            runtime = next;
            PreviewRuntimeTracker.Track(next);
            isRunning = true;
            isPaused = false;
            lastTime = -1d;
            lastError = null;
            lastValidation = null;

            if (previous != null)
            {
                PreviewRuntimeTracker.Untrack(previous);
                SafeDispose(previous);
            }

            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Advances the preview clock. Call once per EditorApplication.update
        /// with EditorApplication.timeSinceStartup.
        /// </summary>
        public void Advance(double timeSinceStartup)
        {
            if (!isRunning || runtime == null || runtime.IsDisposed)
                return;

            if (lastTime < 0d)
            {
                lastTime = timeSinceStartup;
                return;
            }

            double delta = timeSinceStartup - lastTime;
            lastTime = timeSinceStartup;

            if (delta <= 0d)
                return;

            // Avoid a huge jump after a domain reload, breakpoint or freeze.
            if (delta > 0.1d)
                delta = 0.1d;

            try
            {
                runtime.Tick((float)(delta * speed));
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                runtime.Pause();
                isPaused = true;
                NotifyChanged();
            }
        }

        public void TogglePause()
        {
            if (!isRunning)
                return;

            if (isPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (!isRunning)
                return;

            runtime?.Pause();
            isPaused = true;
            NotifyChanged();
        }

        public void Resume()
        {
            if (!isRunning)
                return;

            runtime?.Resume();
            isPaused = false;
            lastTime = -1d;
            NotifyChanged();
        }

        public void Step(float deltaTime = 1f / 60f)
        {
            if (!isRunning || runtime == null || runtime.IsDisposed)
                return;

            try
            {
                runtime.Step(deltaTime);
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                runtime.Pause();
                isPaused = true;
                NotifyChanged();
            }
        }

        public GraphRuntimeSnapshot GetSnapshot()
        {
            if (runtime == null || runtime.IsDisposed)
                return null;

            try
            {
                return runtime.GetSnapshot();
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                return null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
            Changed = null;
        }

        private GraphRuntime CreateRuntime()
        {
            return new GraphRuntime(model, new GraphRuntimeOptions
            {
                Host = host,
            });
        }

        private void ClearError()
        {
            lastError = null;
            lastValidation = null;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static void SafeDispose(GraphRuntime target)
        {
            if (target == null)
                return;

            try
            {
                target.Dispose();
            }
            catch
            {
                // Disposal failures should not break the editor UI.
            }
        }
    }
}
