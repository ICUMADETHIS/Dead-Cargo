using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// A pool of screen markers placed over world positions each frame. Targets on screen get a diamond; targets
    /// off screen, when their style allows, get an arrow clamped to the screen edge, pointing the way.
    /// Call <see cref="Begin"/>, then <see cref="Add"/> per target, then <see cref="End"/>. No per-frame allocations:
    /// a label is only rebuilt when its text or number changes.
    /// </summary>
    internal sealed class HudMarkerLayer
    {
        // Distance of edge arrows from the screen border, in reference pixels.
        private const float EdgeInset = 56f;
        private const float LabelWidth = 220f;
        private const float LabelHeight = 20f;
        private const int LabelSize = 15;
        private const float LabelGap = 6f;

        // Off-screen labels sit this far in from the arrow, towards the screen centre, in reference pixels.
        private const float EdgeLabelOffset = 46f;

        // Gap between an edge arrow and the HUD plates, in reference pixels.
        private const float PlateClearance = 30f;

        // Keeps the clear area around the screen centre from collapsing when the plates cover a lot, in reference pixels.
        private const float MinClearExtent = 1f;

        private sealed class Marker
        {
            public RectTransform Root;
            public HudMarkerGlyph Glyph;
            public Text Label;
            public string ShownText;
            public int ShownNumber = int.MinValue;
        }

        private readonly RectTransform layer;
        private readonly List<Marker> markers;
        private Camera viewCamera;
        private Vector2 canvasSize;
        private Vector2 coveredCorner;
        private int used;

        internal HudMarkerLayer(RectTransform parent, int capacity)
        {
            layer = HudFactory.Stretch("Markers", parent);
            markers = new List<Marker>(capacity);
            for (int i = 0; i < capacity; ++i)
                markers.Add(CreateMarker(i));
        }

        /// <summary>
        /// Sets the size of the bottom-right screen corner covered by HUD plates, in reference pixels.
        /// Edge arrows stop short of it, so the plates never hide them. Zero when nothing is covered.
        /// </summary>
        internal void SetCoveredCorner(Vector2 size)
        {
            coveredCorner = size;
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
        /// Places the next marker over a world position. The label reads "<paramref name="text"/>  <paramref name="metres"/> m",
        /// or is hidden when <paramref name="text"/> is null. Returns false when the target is not shown.
        /// </summary>
        internal bool Add(Vector3 worldPosition, in HudMarkerStyle style, Color color, string text, int metres, float alpha = 1f)
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
            marker.Glyph.SetArrow(!onScreen);
            marker.Glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 size = new Vector2(style.Size, style.Size);
            if (marker.Glyph.rectTransform.sizeDelta != size)
                marker.Glyph.rectTransform.sizeDelta = size;
            color.a *= alpha;
            marker.Glyph.color = color;

            PlaceLabel(marker, text, metres, onScreen, position, style.Size, color);
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

        // How far along its bearing from the screen centre an edge arrow sits. It stops at the screen border or,
        // towards the lower right, short of the plates. The clear area is two overlapping rectangles: everything
        // left of the plates, and everything above them. The arrow goes to the farther of the two exits.
        private float EdgeScale(Vector2 direction)
        {
            Vector2 half = canvasSize * 0.5f - new Vector2(EdgeInset, EdgeInset);
            if (coveredCorner.x <= 0f || coveredCorner.y <= 0f)
                return ExitScale(direction, -half.x, half.x, -half.y, half.y);

            float right = Mathf.Clamp(canvasSize.x * 0.5f - coveredCorner.x - PlateClearance, MinClearExtent, half.x);
            float bottom = Mathf.Clamp(coveredCorner.y + PlateClearance - canvasSize.y * 0.5f, -half.y, -MinClearExtent);
            return Mathf.Max(
                ExitScale(direction, -half.x, right, -half.y, half.y),
                ExitScale(direction, -half.x, half.x, bottom, half.y));
        }

        // Scale at which a ray from the centre along a non-zero direction leaves a rectangle around the centre.
        private static float ExitScale(Vector2 direction, float left, float right, float bottom, float top)
        {
            float x = direction.x > 0f ? right / direction.x : direction.x < 0f ? left / direction.x : float.PositiveInfinity;
            float y = direction.y > 0f ? top / direction.y : direction.y < 0f ? bottom / direction.y : float.PositiveInfinity;
            return Mathf.Min(x, y);
        }

        private static void PlaceLabel(Marker marker, string text, int metres, bool onScreen, Vector2 position, float glyphSize, Color color)
        {
            bool visible = text != null;
            marker.Label.enabled = visible;
            if (!visible)
                return;

            if (!ReferenceEquals(text, marker.ShownText) || metres != marker.ShownNumber)
            {
                marker.ShownText = text;
                marker.ShownNumber = metres;
                marker.Label.text = $"{text}  {metres} m";
            }

            marker.Label.color = color;
            marker.Label.rectTransform.anchoredPosition = onScreen
                ? new Vector2(0f, -(glyphSize * 0.5f + LabelGap + LabelHeight * 0.5f))
                : -position.normalized * EdgeLabelOffset;
        }

        private Marker CreateMarker(int index)
        {
            RectTransform root = HudFactory.Anchored($"Marker {index}", layer, new Vector2(0.5f, 0.5f), 0f, 0f);
            RectTransform glyphRect = HudFactory.Anchored("Glyph", root, new Vector2(0.5f, 0.5f), 16f, 16f);
            var glyph = glyphRect.gameObject.AddComponent<HudMarkerGlyph>();
            glyph.raycastTarget = false;

            Text label = HudFactory.CentredLabel("Label", root, LabelWidth, LabelHeight, LabelSize, HudFactory.Ink, true);
            root.gameObject.SetActive(false);
            return new Marker { Root = root, Glyph = glyph, Label = label };
        }
    }
}
