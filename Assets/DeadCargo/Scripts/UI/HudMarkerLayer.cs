using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.UI
{
    /// <summary>
    /// A pool of screen markers placed over world positions each frame, in the comic look: an inked diamond over a target
    /// on screen, or an inked arrowhead on the screen edge pointing the way to one off screen, when its style allows. Each
    /// can carry a sticker label (<see cref="HudMarkerLabel"/>) with its name, state and distance.
    /// Call <see cref="Begin"/>, then <see cref="Add"/> per target, then <see cref="End"/>. No per-frame allocations:
    /// a label is only rebuilt when its text or number changes.
    /// </summary>
    internal sealed class HudMarkerLayer
    {
        // Distance of edge arrows from the screen border, in reference pixels.
        private const float EdgeInset = 56f;

        // Gap between a glyph and its label, in reference pixels.
        private const float LabelGap = 8f;

        // A label on screen goes to the glyph's left when it would come this close to the screen's right edge.
        private const float LabelEdgeMargin = 16f;

        // Gap between an edge arrow and the HUD plates, in reference pixels.
        private const float PlateClearance = 30f;

        // Keeps the clear area around the screen centre from collapsing when the plates cover a lot, in reference pixels.
        private const float MinClearExtent = 1f;

        private sealed class Marker
        {
            public RectTransform Root;
            public HudMarkerGlyph Glyph;
            public HudMarkerLabel Label;
            public string ShownText;
            public int ShownNumber = int.MinValue;
            public string Figure;
        }

        private readonly RectTransform layer;
        private readonly List<Marker> markers;
        private Camera viewCamera;
        private Vector2 canvasSize;
        private Vector2 coveredCorner;
        private float coveredBottom;
        private int used;

        internal HudMarkerLayer(RectTransform parent, int capacity)
        {
            layer = HudFactory.Stretch("Markers", parent);
            markers = new List<Marker>(capacity);
            for (int i = 0; i < capacity; ++i)
                markers.Add(CreateMarker(i));
        }

        /// <summary>
        /// Sets the size of the bottom-right screen corner covered by HUD tags, in reference pixels.
        /// Edge arrows stop short of it, so the tags never hide them. Zero when nothing is covered.
        /// </summary>
        internal void SetCoveredCorner(Vector2 size)
        {
            coveredCorner = size;
        }

        /// <summary>
        /// Sets the height of the band along the screen's bottom covered by the status strip, in reference pixels. Edge
        /// arrows stay above it. Zero when nothing is covered.
        /// </summary>
        internal void SetCoveredBottom(float height)
        {
            coveredBottom = Mathf.Max(0f, height);
        }

        /// <summary>Starts a frame of markers. Returns false, and shows nothing, when there is no camera.</summary>
        internal bool Begin(Camera camera)
        {
            viewCamera = camera;
            used = 0;
            canvasSize = layer.rect.size;
            return viewCamera != null;
        }

        /// <summary>
        /// Places the next marker over a world position. Its label names the target, adds <paramref name="detail"/> when
        /// the style is loud, and gives the distance in metres, or kilometres when the style says so. A null
        /// <paramref name="text"/> or a plain style shows the glyph alone. Returns false when the target is not shown.
        /// </summary>
        internal bool Add(Vector3 worldPosition, in HudMarkerStyle style, Color color, string text, int metres, float alpha = 1f,
            string detail = null)
        {
            if (used >= markers.Count)
                return false;

            // Centred viewport space: -0.5 to 0.5 on screen. Points behind the camera are mirrored so arrows point the right way.
            Vector3 viewport = viewCamera.WorldToViewportPoint(worldPosition);
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            bool behind = viewport.z < 0f;
            if (behind)
                direction = -direction;

            bool onScreen = !behind && Mathf.Abs(direction.x) <= 0.5f && Mathf.Abs(direction.y) <= 0.5f;
            if (!onScreen && !style.ShowOffScreen)
                return false;

            Marker marker = markers[used++];
            Vector2 position = Vector2.Scale(direction, canvasSize);
            float angle = 0f;
            if (!onScreen)
            {
                if (position.sqrMagnitude < 0.0001f)
                    position = Vector2.down;

                position *= EdgeScale(position);
                angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            }

            if (!marker.Root.gameObject.activeSelf)
                marker.Root.gameObject.SetActive(true);

            marker.Root.anchoredPosition = position;
            HudMarkerGlyph glyph = marker.Glyph;
            glyph.SetShape(!onScreen, style.Outline);
            glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 size = new Vector2(style.Size, style.Size);
            if (glyph.rectTransform.sizeDelta != size)
                glyph.rectTransform.sizeDelta = size;

            color.a *= alpha;
            glyph.color = color;

            PlaceLabel(marker, style, color, text, detail, metres, onScreen, position);
            return true;
        }

        /// <summary>Hides the markers this frame did not use.</summary>
        internal void End()
        {
            for (int i = used; i < markers.Count; ++i)
            {
                if (markers[i].Root.gameObject.activeSelf)
                    markers[i].Root.gameObject.SetActive(false);
            }
        }

        /// <summary>Hides every marker.</summary>
        internal void Hide()
        {
            used = 0;
            End();
        }

        // How far along its bearing from the screen centre an edge arrow sits. It stops at the screen border, above the
        // status strip or, towards the lower right, short of the tags. The clear area is two overlapping rectangles:
        // everything left of the tags, and everything above them. The arrow goes to the farther of the two exits.
        private float EdgeScale(Vector2 direction)
        {
            Vector2 half = canvasSize * 0.5f - new Vector2(EdgeInset, EdgeInset);
            float floor = coveredBottom > 0f
                ? Mathf.Min(-MinClearExtent, Mathf.Max(-half.y, coveredBottom + PlateClearance - canvasSize.y * 0.5f))
                : -half.y;
            if (coveredCorner.x <= 0f || coveredCorner.y <= 0f)
                return ExitScale(direction, -half.x, half.x, floor, half.y);

            float right = Mathf.Clamp(canvasSize.x * 0.5f - coveredCorner.x - PlateClearance, MinClearExtent, half.x);
            float bottom = Mathf.Clamp(coveredCorner.y + PlateClearance - canvasSize.y * 0.5f, floor, -MinClearExtent);
            return Mathf.Max(
                ExitScale(direction, -half.x, right, floor, half.y),
                ExitScale(direction, -half.x, half.x, bottom, half.y));
        }

        // Scale at which a ray from the centre along a non-zero direction leaves a rectangle around the centre.
        private static float ExitScale(Vector2 direction, float left, float right, float bottom, float top)
        {
            float x = direction.x > 0f ? right / direction.x : direction.x < 0f ? left / direction.x : float.PositiveInfinity;
            float y = direction.y > 0f ? top / direction.y : direction.y < 0f ? bottom / direction.y : float.PositiveInfinity;
            return Mathf.Min(x, y);
        }

        // On screen the label sits beside the glyph, to its right unless that runs off the screen. Off screen it sits
        // inwards of the arrow, towards the screen centre, clear of the arrow whichever edge it is on.
        private void PlaceLabel(Marker marker, in HudMarkerStyle style, Color color, string text, string detail, int metres,
            bool onScreen, Vector2 position)
        {
            HudMarkerLabel label = marker.Label;
            bool visible = text != null && style.Look != HudMarkerLook.Plain;
            label.SetVisible(visible);
            if (!visible)
                return;

            // Kilometre figures are stored negated, so a marker that changes unit always rebuilds its figure.
            int shown = style.InKilometres ? -metres - 1 : metres;
            if (!ReferenceEquals(text, marker.ShownText) || shown != marker.ShownNumber || marker.Figure == null)
            {
                marker.ShownText = text;
                marker.ShownNumber = shown;
                marker.Figure = style.InKilometres ? $"{metres:N0} KM" : $"{metres:N0} M";
            }

            color.a = 1f;
            label.Set(style.Look, color, text, detail, marker.Figure);

            Vector2 size = label.Size;
            float reach = marker.Glyph.Reach + LabelGap;
            if (onScreen)
            {
                float side = position.x + reach + size.x > canvasSize.x * 0.5f - LabelEdgeMargin ? -1f : 1f;
                label.SetPosition(new Vector2(side * (reach + size.x * 0.5f), 0f));
                return;
            }

            Vector2 inward = -position.normalized;
            float extent = Mathf.Abs(inward.x) * size.x * 0.5f + Mathf.Abs(inward.y) * size.y * 0.5f;
            label.SetPosition(inward * (reach + extent));
        }

        private Marker CreateMarker(int index)
        {
            RectTransform root = HudFactory.Anchored($"Marker {index}", layer, new Vector2(0.5f, 0.5f), 0f, 0f);
            RectTransform glyphRect = HudFactory.Anchored("Glyph", root, new Vector2(0.5f, 0.5f), 16f, 16f);
            var glyph = glyphRect.gameObject.AddComponent<HudMarkerGlyph>();
            var label = new HudMarkerLabel(root);
            root.gameObject.SetActive(false);
            return new Marker { Root = root, Glyph = glyph, Label = label };
        }
    }
}
