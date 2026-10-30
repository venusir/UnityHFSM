using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    internal static class GraphEditorStyles
    {
        public const float HeaderHeight = 24f;

        public static Color HeaderBackgroundColor => EditorGUIUtility.isProSkin
            ? new Color(0.26f, 0.26f, 0.26f)
            : new Color(0.82f, 0.82f, 0.82f);

        public static Color SeparatorColor => EditorGUIUtility.isProSkin
            ? new Color(0.14f, 0.14f, 0.14f)
            : new Color(0.58f, 0.58f, 0.58f);

        public static Color MutedTextColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.55f)
            : new Color(0f, 0f, 0f, 0.55f);

        public static Color ErrorColor => EditorGUIUtility.isProSkin
            ? new Color(0.95f, 0.42f, 0.42f)
            : new Color(0.70f, 0.15f, 0.15f);

        public static Color WarningColor => EditorGUIUtility.isProSkin
            ? new Color(0.95f, 0.75f, 0.30f)
            : new Color(0.55f, 0.38f, 0.00f);

        public static Color SuccessColor => EditorGUIUtility.isProSkin
            ? new Color(0.45f, 0.85f, 0.45f)
            : new Color(0.15f, 0.50f, 0.15f);

        public static VisualElement CreateHeaderBand(string title, out VisualElement rightSlot)
        {
            VisualElement header = CreateHeaderBand(out VisualElement leftSlot, out rightSlot);

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 1f;
            label.style.minWidth = 0f;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.overflow = Overflow.Hidden;
            leftSlot.Add(label);

            return header;
        }

        public static VisualElement CreateHeaderBand(out VisualElement leftSlot, out VisualElement rightSlot)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = HeaderHeight;
            header.style.minHeight = HeaderHeight;
            header.style.maxHeight = HeaderHeight;
            header.style.flexShrink = 0f;
            header.style.paddingLeft = 8f;
            header.style.paddingRight = 6f;
            header.style.backgroundColor = new StyleColor(HeaderBackgroundColor);
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new StyleColor(SeparatorColor);
            header.style.overflow = Overflow.Hidden;

            leftSlot = new VisualElement();
            leftSlot.style.flexDirection = FlexDirection.Row;
            leftSlot.style.alignItems = Align.Center;
            leftSlot.style.flexGrow = 1f;
            leftSlot.style.flexShrink = 1f;
            leftSlot.style.minWidth = 0f;
            leftSlot.style.overflow = Overflow.Hidden;
            header.Add(leftSlot);

            rightSlot = new VisualElement();
            rightSlot.style.flexDirection = FlexDirection.Row;
            rightSlot.style.alignItems = Align.Center;
            rightSlot.style.flexShrink = 1f;
            rightSlot.style.minWidth = 0f;
            rightSlot.style.marginLeft = 6f;
            header.Add(rightSlot);

            return header;
        }

        public static void SetBorder(
            VisualElement element,
            Color color,
            float leftWidth = 0f,
            float rightWidth = 0f,
            float topWidth = 0f,
            float bottomWidth = 0f)
        {
            if (leftWidth > 0f)
            {
                element.style.borderLeftWidth = leftWidth;
                element.style.borderLeftColor = new StyleColor(color);
            }

            if (rightWidth > 0f)
            {
                element.style.borderRightWidth = rightWidth;
                element.style.borderRightColor = new StyleColor(color);
            }

            if (topWidth > 0f)
            {
                element.style.borderTopWidth = topWidth;
                element.style.borderTopColor = new StyleColor(color);
            }

            if (bottomWidth > 0f)
            {
                element.style.borderBottomWidth = bottomWidth;
                element.style.borderBottomColor = new StyleColor(color);
            }
        }

        public static Label CreateMutedLabel(string text = "")
        {
            var label = new Label(text);
            label.style.color = new StyleColor(MutedTextColor);
            label.style.fontSize = 11f;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.overflow = Overflow.Hidden;
            label.style.flexShrink = 1f;
            label.style.minWidth = 0f;
            return label;
        }

        public static Label CreateBreadcrumbSeparator()
        {
            Label label = CreateMutedLabel(">");
            label.style.marginLeft = 2f;
            label.style.marginRight = 2f;
            label.style.flexShrink = 0f;
            return label;
        }

        public static Button CreateBreadcrumbButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.height = 18f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            button.style.fontSize = 11f;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.textOverflow = TextOverflow.Ellipsis;
            button.style.overflow = Overflow.Hidden;
            button.style.maxWidth = 180f;
            button.style.flexShrink = 1f;
            button.style.minWidth = 0f;
            return button;
        }

        public static Label CreateStatusLabel()
        {
            Label label = CreateMutedLabel();
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            label.style.maxWidth = 260f;
            label.style.marginRight = 4f;
            return label;
        }
    }
}
