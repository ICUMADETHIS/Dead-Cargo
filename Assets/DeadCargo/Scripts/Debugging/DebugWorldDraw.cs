#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System.Collections.Generic;
using System.Text;
using DDT;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using DeadCargo.Extraction;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// The channels' drawing in the world, through Draw Debug Tools. What to draw and every label's text are taken a few
    /// times a second in <see cref="Refresh"/>; positions are read every frame in <see cref="Draw"/>, so labels never
    /// lag a fast ship and no text is built per frame. Only things within range of the player are drawn.
    /// </summary>
    internal sealed class DebugWorldDraw
    {
        private const float Range = 2500f;
        private const float PhysicsRange = 250f;
        private const int MaxPhysicsColliders = 256;
        private const int MaxPhysicsBodies = 96;
        private const float SceneScanInterval = 1f;

        // Label size per metre of distance from the camera, so labels read the same near and far.
        private const float LabelScale = 0.012f;

        private static readonly Color PlayerColor = new Color(0.45f, 0.85f, 1f);
        private static readonly Color VelocityColor = new Color(0.43f, 0.85f, 0.54f);
        private static readonly Color TargetColor = new Color(1f, 0.42f, 0.37f);
        private static readonly Color AiColor = new Color(1f, 0.6f, 0.3f);
        private static readonly Color DormantColor = new Color(0.56f, 0.59f, 0.64f);
        private static readonly Color UplinkColor = new Color(1f, 0.82f, 0.3f);
        private static readonly Color OpenColor = new Color(0.43f, 0.85f, 0.54f);
        private static readonly Color PickupColor = new Color(0.94f, 0.74f, 0.44f);
        private static readonly Color RockColor = new Color(0.94f, 0.74f, 0.44f);
        private static readonly Color PieceColor = new Color(0.7f, 0.53f, 1f);
        private static readonly Color SpinColor = new Color(1f, 0.36f, 0.8f);
        private static readonly Color AwakeColor = new Color(0.43f, 0.85f, 0.54f);
        private static readonly Color AsleepColor = new Color(0.45f, 0.47f, 0.5f);
        private static readonly Color KinematicColor = new Color(0.45f, 0.7f, 1f);

        private readonly StringBuilder label = new StringBuilder(64);
        private readonly List<Vehicle> ships = new List<Vehicle>();
        private readonly List<string> shipLabels = new List<string>();
        private readonly List<ExtractionPoint> points = new List<ExtractionPoint>();
        private readonly List<string> pointLabels = new List<string>();
        private readonly List<CargoPickup> pickups = new List<CargoPickup>();
        private readonly List<string> pickupLabels = new List<string>();
        private readonly List<VoxelBody> rocks = new List<VoxelBody>();
        private readonly List<string> rockLabels = new List<string>();
        private readonly Collider[] overlap = new Collider[MaxPhysicsColliders];
        private readonly HashSet<Rigidbody> seenBodies = new HashSet<Rigidbody>();
        private CargoPickup[] sceneScan = new CargoPickup[0];
        private float nextSceneScan;
        private string playerLabel;

        /// <summary>Takes what to draw and builds every label. Call a few times a second.</summary>
        public void Refresh(DebugContext context)
        {
            if (DebugSettings.IsOn(DebugChannel.Player))
                RefreshPlayer(context);
            if (DebugSettings.IsOn(DebugChannel.Ai))
                RefreshShips(context);
            if (DebugSettings.IsOn(DebugChannel.Gameplay))
                RefreshGameplay(context);
            if (DebugSettings.IsOn(DebugChannel.Destruction))
                RefreshRocks(context);
        }

        /// <summary>Draws every enabled channel. Call every frame.</summary>
        public void Draw(DebugContext context)
        {
            if (DebugSettings.IsOn(DebugChannel.Player))
                DrawPlayer(context);
            if (DebugSettings.IsOn(DebugChannel.Ai))
                DrawShips(context);
            if (DebugSettings.IsOn(DebugChannel.Gameplay))
                DrawGameplay(context);
            if (DebugSettings.IsOn(DebugChannel.Destruction))
                DrawRocks(context);

            context.NearbyBodies = DebugSettings.IsOn(DebugChannel.Physics) ? DrawPhysics(context) : 0;
        }

        private void RefreshPlayer(DebugContext context)
        {
            Vehicle ship = context.Player;
            if (ship == null)
            {
                playerLabel = null;
                return;
            }

            Rigidbody body = ship.CachedRigidbody;
            label.Clear().Append(ship.Label).Append('\n').Number(body != null ? body.linearVelocity.magnitude : 0f, 1).Append(" m/s");
            playerLabel = label.ToString();
        }

        private void DrawPlayer(DebugContext context)
        {
            Vehicle ship = context.Player;
            if (ship == null)
                return;

            Transform view = ship.transform;
            Vector3 position = view.position;
            Rigidbody body = ship.CachedRigidbody;
            float size = LabelSize(context, position);

            DrawDebugTools.Draw3DCoordinates(position, view.rotation, size * 4f);
            DrawDebugTools.DrawLine(position, position + view.forward * (size * 25f), PlayerColor);
            if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
                DrawDebugTools.DrawDirectionalArrow(position, position + body.linearVelocity, size, VelocityColor);

            var target = context.PlayerTargets != null ? context.PlayerTargets.SelectedTarget : null;
            if (target != null)
                DrawDebugTools.DrawDistance(position, target.transform.position, TargetColor, size);

            if (playerLabel != null)
                DrawDebugTools.DrawString3D(position + view.up * (size * 3f), playerLabel, TextAnchor.LowerCenter, PlayerColor, size);
        }

        private void RefreshShips(DebugContext context)
        {
            ships.Clear();
            shipLabels.Clear();
            AuthorityResponse authority = AuthorityResponse.Active;
            if (authority == null)
                return;

            foreach (Vehicle ship in authority.ActiveShips)
            {
                if (ship == null || ship.Destroyed || (ship.transform.position - context.Focus).sqrMagnitude > Range * Range)
                    continue;

                Rigidbody body = ship.CachedRigidbody;
                label.Clear().Append(ship.Label).Append('\n').Number(body != null ? body.linearVelocity.magnitude : 0f).Append(" m/s  ")
                    .Number(Vector3.Distance(context.Focus, ship.transform.position)).Append(" m");
                ships.Add(ship);
                shipLabels.Add(label.ToString());
            }
        }

        private void DrawShips(DebugContext context)
        {
            Vector3 player = context.Player != null ? context.Player.transform.position : context.Focus;
            for (int i = 0; i < ships.Count; ++i)
            {
                Vehicle ship = ships[i];
                if (ship == null || ship.Destroyed)
                    continue;

                Vector3 position = ship.transform.position;
                float size = LabelSize(context, position);
                Rigidbody body = ship.CachedRigidbody;
                if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
                    DrawDebugTools.DrawDirectionalArrow(position, position + body.linearVelocity, size, AiColor);

                // Authority ships hunt the player, so the line shows who is closing in.
                if (context.Player != null)
                    DrawDebugTools.DrawLine(position, player, new Color(AiColor.r, AiColor.g, AiColor.b, 0.35f));

                DrawDebugTools.DrawString3D(position + Vector3.up * (size * 3f), shipLabels[i], TextAnchor.LowerCenter, AiColor, size);
            }
        }

        private void RefreshGameplay(DebugContext context)
        {
            points.Clear();
            pointLabels.Clear();
            foreach (ExtractionPoint point in ExtractionPoint.All)
            {
                if (point == null)
                    continue;

                label.Clear().Append("EXTRACTION\n").Append(point.State == ExtractionPointState.Open ? "open" :
                    point.State == ExtractionPointState.Uplinking ? "uplinking" : "dormant").Append("  ")
                    .Number(point.DistanceTo(context.Focus)).Append(" m");
                points.Add(point);
                pointLabels.Add(label.ToString());
            }

            // Pickups have no registry, so the scene is searched once a second rather than every refresh.
            if (Time.unscaledTime >= nextSceneScan)
            {
                nextSceneScan = Time.unscaledTime + SceneScanInterval;
                sceneScan = Object.FindObjectsByType<CargoPickup>(FindObjectsSortMode.None);
            }

            pickups.Clear();
            pickupLabels.Clear();
            foreach (CargoPickup pickup in sceneScan)
            {
                if (pickup == null || !pickup.isActiveAndEnabled || pickup.Item == null ||
                    (pickup.transform.position - context.Focus).sqrMagnitude > Range * Range)
                    continue;

                label.Clear().Append(pickup.Item.DisplayName).Append(" x").Integer(pickup.Quantity);
                pickups.Add(pickup);
                pickupLabels.Add(label.ToString());
            }
        }

        private void DrawGameplay(DebugContext context)
        {
            for (int i = 0; i < points.Count; ++i)
            {
                ExtractionPoint point = points[i];
                if (point == null)
                    continue;

                Vector3 position = point.transform.position;
                Color color = point.State == ExtractionPointState.Open ? OpenColor :
                    point.State == ExtractionPointState.Uplinking ? UplinkColor : DormantColor;
                DrawDebugTools.DrawSphere(position, point.EntryRadius, 16, color);
                DrawDebugTools.DrawCircle(position, Quaternion.identity, point.ActivationRange, 48, new Color(color.r, color.g, color.b, 0.4f));
                float size = LabelSize(context, position);
                DrawDebugTools.DrawString3D(position + Vector3.up * (point.EntryRadius + size * 2f), pointLabels[i], TextAnchor.LowerCenter, color, size);
            }

            for (int i = 0; i < pickups.Count; ++i)
            {
                CargoPickup pickup = pickups[i];
                if (pickup == null || !pickup.isActiveAndEnabled)
                    continue;

                Vector3 position = pickup.transform.position;
                float size = LabelSize(context, position);
                DrawDebugTools.DrawPoint(position, size * 2f, PickupColor);
                DrawDebugTools.DrawString3D(position + Vector3.up * (size * 2f), pickupLabels[i], TextAnchor.LowerCenter, PickupColor, size * 0.8f);
            }
        }

        private void RefreshRocks(DebugContext context)
        {
            rocks.Clear();
            rockLabels.Clear();
            DestructionSystem system = DestructionSystem.Active;
            if (system == null)
                return;

            foreach (VoxelBody body in system.Bodies)
            {
                if (body == null || !body.IsAllocated || body.Mass <= 0f || (body.CentreOfMass - context.Focus).sqrMagnitude > Range * Range)
                    continue;

                label.Clear().Append(body.IsPiece ? "PIECE" : "ROCK").Append('\n').Number(body.Mass).Append(" t  ")
                    .Number(body.LinearVelocity.magnitude, 2).Append(" m/s  ").Number(body.AngularVelocity.magnitude * Mathf.Rad2Deg, 1).Append(" deg/s");
                rocks.Add(body);
                rockLabels.Add(label.ToString());
            }
        }

        private void DrawRocks(DebugContext context)
        {
            for (int i = 0; i < rocks.Count; ++i)
            {
                VoxelBody body = rocks[i];
                if (body == null || !body.IsAllocated)
                    continue;

                Vector3 centre = body.CentreOfMass;
                float radius = Mathf.Pow(Mathf.Max(body.Volume, 0f) * (3f / (4f * Mathf.PI)), 1f / 3f);
                float size = LabelSize(context, centre);
                Color color = body.IsPiece ? PieceColor : RockColor;

                DrawDebugTools.DrawPoint(centre, size * 2f, color);

                // Drift is too slow to see at its true length, so it is drawn as ten seconds of travel.
                Vector3 drift = body.LinearVelocity * 10f;
                if (drift.sqrMagnitude > 0.01f)
                    DrawDebugTools.DrawDirectionalArrow(centre, centre + drift, size, VelocityColor);

                // The spin axis, as long as the rock is wide.
                Vector3 spin = body.AngularVelocity;
                if (spin.sqrMagnitude > 1e-6f)
                    DrawDebugTools.DrawLine(centre - spin.normalized * radius, centre + spin.normalized * radius, SpinColor);

                DrawDebugTools.DrawString3D(centre + Vector3.up * (radius + size * 2f), rockLabels[i], TextAnchor.LowerCenter, color, size);
            }
        }

        // Rigidbodies around the camera: each collider's bounds in the colour of its body's state, and its velocity.
        private int DrawPhysics(DebugContext context)
        {
            if (context.Camera == null)
                return 0;

            int hits = Physics.OverlapSphereNonAlloc(context.Camera.transform.position, PhysicsRange, overlap, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            seenBodies.Clear();
            for (int i = 0; i < hits; ++i)
            {
                Collider collider = overlap[i];
                Rigidbody body = collider.attachedRigidbody;
                if (body == null)
                    continue;

                Color color = body.isKinematic ? KinematicColor : body.IsSleeping() ? AsleepColor : AwakeColor;
                DrawDebugTools.DrawBounds(collider.bounds, color);
                if (seenBodies.Count >= MaxPhysicsBodies || !seenBodies.Add(body))
                    continue;

                Vector3 velocity = body.isKinematic ? Vector3.zero : body.linearVelocity;
                if (velocity.sqrMagnitude > 0.01f)
                    DrawDebugTools.DrawDirectionalArrow(body.worldCenterOfMass, body.worldCenterOfMass + velocity, 0.5f, color);
            }

            return seenBodies.Count;
        }

        private static float LabelSize(DebugContext context, Vector3 position)
        {
            Vector3 eye = context.Camera != null ? context.Camera.transform.position : context.Focus;
            return Mathf.Max(0.3f, Vector3.Distance(eye, position) * LabelScale);
        }
    }
}
#endif
