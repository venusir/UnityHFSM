using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Thin draggable separator that resizes one target pane. Custom instead of
    /// TwoPaneSplitView because the window needs min/max clamps, double-click
    /// reset, panel hiding and serialized sizes.
    /// </summary>
    public sealed class ResizableSplitter : VisualElement
    {
        public enum SplitOrientation
        {
            Vertical = 0,
            Horizontal = 1,
        }

        private readonly SplitOrientation orientation;
        private readonly VisualElement target;
        private readonly bool targetBeforeSplitter;
        private readonly float minSize;
        private readonly Func<float> maxSizeProvider;
        private readonly float defaultSize;
        private readonly Action<float> sizeChanged;
        private readonly VisualElement line;

        private bool dragging;
        private int pointerId = -1;
        private float currentSize;

        public ResizableSplitter(
            SplitOrientation orientation,
            VisualElement target,
            bool targetBeforeSplitter,
            float defaultSize,
            float minSize,
            Func<float> maxSizeProvider,
            Action<float> sizeChanged = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            this.orientation = orientation;
            this.target = target;
            this.targetBeforeSplitter = targetBeforeSplitter;
            this.defaultSize = defaultSize;
            this.minSize = minSize;
            this.maxSizeProvider = maxSizeProvider;
            this.sizeChanged = sizeChanged;

            AddToClassList("unityhfsm-splitter");
            pickingMode = PickingMode.Position;
            style.flexGrow = 0f;
            style.flexShrink = 0f;
            tooltip = "Drag to resize. Double-click to reset.";

            line = new VisualElement();
            line.pickingMode = PickingMode.Ignore;
            line.style.position = Position.Absolute;
            line.style.backgroundColor = new StyleColor(GraphEditorStyles.SeparatorColor);

            if (orientation == SplitOrientation.Vertical)
            {
                style.width = 6f;
                line.style.left = 2f;
                line.style.top = 0f;
                line.style.bottom = 0f;
                line.style.width = 1f;
            }
            else
            {
                style.height = 6f;
                line.style.left = 0f;
                line.style.right = 0f;
                line.style.top = 2f;
                line.style.height = 1f;
            }

            Add(line);

            currentSize = Mathf.Max(minSize, defaultSize);
            ApplySize(currentSize, false);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<PointerEnterEvent>(_ => SetHover(true));
            RegisterCallback<PointerLeaveEvent>(_ => SetHover(false));
        }

        public float CurrentSize => currentSize;
        public float DefaultSize => defaultSize;

        public void SetSize(float value)
        {
            ApplySize(value, true);
        }

        public void SetSizeWithoutNotify(float value)
        {
            ApplySize(value, false);
        }

        public void ResetToDefault()
        {
            SetSize(defaultSize);
        }

        private void ApplySize(float value, bool notify)
        {
            float max = ResolveMaxSize();
            float clamped = Mathf.Clamp(value, minSize, max);

            currentSize = clamped;

            if (orientation == SplitOrientation.Vertical)
                target.style.width = clamped;
            else
                target.style.height = clamped;

            if (notify)
                sizeChanged?.Invoke(clamped);
        }

        private float ResolveMaxSize()
        {
            float max = maxSizeProvider != null ? maxSizeProvider() : float.PositiveInfinity;
            if (float.IsNaN(max) || max < minSize)
                return minSize;

            return max;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            if (evt.clickCount == 2)
            {
                ResetToDefault();
                evt.StopPropagation();
                return;
            }

            dragging = true;
            pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            float delta = orientation == SplitOrientation.Vertical
                ? evt.deltaPosition.x
                : evt.deltaPosition.y;

            float next = currentSize + (targetBeforeSplitter ? delta : -delta);
            SetSize(next);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
                return;

            EndDrag();
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
                EndDrag();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == pointerId)
                EndDrag();
        }

        private void EndDrag()
        {
            if (!dragging)
                return;

            dragging = false;

            if (pointerId >= 0)
            {
                if (this.HasPointerCapture(pointerId))
                    this.ReleasePointer(pointerId);

                pointerId = -1;
            }
        }

        private void SetHover(bool hover)
        {
            line.style.backgroundColor = new StyleColor(hover
                ? GraphEditorStyles.MutedTextColor
                : GraphEditorStyles.SeparatorColor);
        }
    }
}
