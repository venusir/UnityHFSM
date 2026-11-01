using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Edge with a durable base color. The GraphView base class resets
    /// EdgeControl colors and width during layout, selection and style
    /// resolution, so the effective color is re-applied after every one of
    /// those passes.
    /// </summary>
    public sealed class TransitionEdge : Edge
    {
        public string TransitionId;
        public bool IsEntryEdge;

        private Color baseColor = new Color(0.55f, 0.55f, 0.55f);
        private Color? highlightColor;
        private int highlightWidth = 1;

        public void SetBaseColor(Color color)
        {
            baseColor = color;
            ApplyColors();
        }

        public void SetColor(Color color)
        {
            SetBaseColor(color);
        }

        public void SetHighlight(Color color, float width)
        {
            highlightColor = color;
            highlightWidth = Mathf.Max(1, Mathf.RoundToInt(width));
            ApplyColors();
        }

        public void ClearHighlight()
        {
            if (!highlightColor.HasValue)
                return;

            highlightColor = null;
            highlightWidth = 1;
            ApplyColors();
        }

        public override bool UpdateEdgeControl()
        {
            bool updated = base.UpdateEdgeControl();
            ApplyColors();
            return updated;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            ApplyColors();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            ApplyColors();
        }

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);
            ApplyColors();
        }

        private void ApplyColors()
        {
            if (edgeControl == null)
                return;

            Color color = highlightColor ?? (selected ? GraphEditorStyles.SelectedColor : baseColor);
            int width = highlightColor.HasValue ? highlightWidth : (selected ? 2 : 1);

            if (edgeControl.inputColor != color)
                edgeControl.inputColor = color;

            if (edgeControl.outputColor != color)
                edgeControl.outputColor = color;

            // Changing edgeWidth forces EdgeControl to recompute its layout,
            // which fails while the edge is still being attached to a port or
            // used outside a panel (for example in EditMode smoke tests).
            if (edgeControl.panel != null && edgeControl.edgeWidth != width)
                edgeControl.edgeWidth = width;
        }
    }
}
