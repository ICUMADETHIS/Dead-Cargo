using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The cel-banded light trail with an ink edge that a ship leaves while streaking, out of a run (<see cref="JumpStreak"/>)
    /// or into one (<see cref="ArrivalStreak"/>). Two trail renderers under one holder: the ink is wider and drawn first, so
    /// it edges the light like an outline.
    /// </summary>
    internal static class StreakTrails
    {
        /// <summary>How long a point of the trail lasts, in seconds.</summary>
        internal const float TrailTime = 0.3f;

        private const float TrailWidth = 4f;
        private const float InkWidth = 1.4f;
        private const string TrailShader = "Sprites/Default";

        private static readonly Color Ink = new Color32(4, 4, 6, 255);
        private static readonly Color Core = new Color32(243, 238, 228, 255);
        private static readonly Color Glow = new Color32(116, 206, 255, 255);
        private static readonly Color Fringe = new Color32(96, 224, 208, 255);

        private static Material trailMaterial;

        private static Material TrailMaterial
        {
            get
            {
                if (trailMaterial == null)
                    trailMaterial = new Material(Shader.Find(TrailShader)) { name = "Streak Trail" };
                return trailMaterial;
            }
        }

        /// <summary>Starts the trails on <paramref name="ship"/>, from where it stands now. Returns their holder.</summary>
        internal static GameObject Add(Transform ship, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(ship, false);
            AddTrail(holder.transform, "Ink", InkGradient(), TrailWidth * InkWidth, 0);
            AddTrail(holder.transform, "Light", LightGradient(), TrailWidth, 1);
            return holder;
        }

        /// <summary>Stops the trails emitting and removes them once their last points have faded.</summary>
        internal static void Stop(GameObject holder)
        {
            if (holder == null)
                return;

            foreach (TrailRenderer trail in holder.GetComponentsInChildren<TrailRenderer>())
                trail.emitting = false;

            Object.Destroy(holder, TrailTime);
        }

        private static void AddTrail(Transform parent, string objectName, Gradient colors, float width, int order)
        {
            var holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);
            TrailRenderer trail = holder.AddComponent<TrailRenderer>();
            trail.time = TrailTime;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = colors;
            trail.sharedMaterial = TrailMaterial;
            trail.sortingOrder = order;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        // Hard colour steps along the trail, as the comic surfaces step their light.
        private static Gradient LightGradient()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[] { new GradientColorKey(Core, 0.3f), new GradientColorKey(Glow, 0.7f), new GradientColorKey(Fringe, 1f) },
                new[] { new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0.6f, 1f) });
            return gradient;
        }

        private static Gradient InkGradient()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(new[] { new GradientColorKey(Ink, 1f) }, new[] { new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0.5f, 1f) });
            return gradient;
        }
    }
}
