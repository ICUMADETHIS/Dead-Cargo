using System.Collections.Generic;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using DeadCargo.DevTesting;
using DeadCargo.EditorTools.Destruction;
using DeadCargo.EditorTools.Diagnostics;
using DeadCargo.Flow;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// Spawn voxel asteroids, hit them, break chunks off them, push and spin them, watch the destruction pipeline's
    /// cost, and run its tests.
    /// </summary>
    internal sealed class DestructionTab : GodMenuTab
    {
        private const float CraterRadius = 2.5f;
        private const float BlastRadius = 7f;
        private const float GunDamage = 100f;
        private const float MissileDamage = 2500f;
        private const int ShatterCracks = 3;
        private const float PushSpeed = 3f;
        private const float SpinDegrees = 20f;
        private const float AimRange = 3000f;
        private const float BytesPerMegabyte = 1024f * 1024f;

        private static readonly RaycastHit[] aimHits = new RaycastHit[32];

        private static readonly string[] SpawnLabels = { "Spawn Ahead", "Clear Pieces", "Remove All" };
        private static readonly string[] HitLabels = { "Crater", "Blast", "Crumble" };
        private static readonly string[] BreakLabels = { "Small Chunk", "Medium Chunk", "Large Chunk", "Random Chunk", "Shatter" };
        private static readonly string[] MotionLabels = { "Push", "Spin", "Stop" };
        private static readonly string[] TestLabels = { "Core Self-Test", "Smoke Test", "Benchmark" };
        private static readonly string[] StatsLabels = { "Reset Stats" };

        private readonly List<AsteroidDefinition> types = new List<AsteroidDefinition>();
        private string[] typeNames = new string[0];
        private int typeIndex;
        private float diameter = 40f;
        private float distance = 120f;

        private DestructionSystem system;

        // The rock the buttons act on: the one in your sights, else the one picked last, else the nearest.
        private VoxelBody target;
        private bool targetInSights;
        private float targetDistance;
        private int rocks;
        private int pieces;
        private int surfaceChunks;
        private int triangles;
        private int halfDetail;
        private int quarterDetail;
        private long voxelBytes;

        public override string Title => "Destruction";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 70;

        public override bool IsAvailable => GodMenuContext.IsPlaying && system != null && !SceneFlow.InMothership;

        public override string Summary
        {
            get
            {
                if (system == null)
                    return null;

                string text = $"{rocks} rocks · {pieces} pieces";
                return system.QueuedChunks > 0 ? $"{text} · {system.QueuedChunks} queued" : text;
            }
        }

        public override void OnUpdate()
        {
            system = GodMenuContext.IsPlaying ? DestructionSystem.Active : null;
            rocks = pieces = surfaceChunks = triangles = halfDetail = quarterDetail = 0;
            voxelBytes = 0;
            if (system == null)
            {
                target = null;
                return;
            }

            if (types.Count == 0)
                RefreshTypes();

            Transform eye = Eye();
            VoxelBody nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (VoxelBody body in system.Bodies)
            {
                if (body == null || !body.IsAllocated || body.IsWarmUp)
                    continue;

                if (body.IsPiece)
                    pieces++;
                else
                    rocks++;

                surfaceChunks += body.SurfaceChunks;
                triangles += body.TriangleCount;
                if (body.DetailLevel == 1)
                    halfDetail++;
                else if (body.DetailLevel >= 2)
                    quarterDetail++;
                voxelBytes += body.MemoryBytes;

                if (eye == null || !body.IsReady)
                    continue;

                float d = SurfaceDistance(body, eye.position);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = body;
                }
            }

            // A rock in your sights becomes the target and stays it after you look away, so breaking a chunk off
            // never moves the target onto the chunk.
            targetInSights = false;
            if (eye == null)
            {
                if (!IsUsable(target))
                    target = null;
                return;
            }

            VoxelBody aimed = InSights(eye, out _, out float aimedDistance);
            if (aimed != null)
            {
                target = aimed;
                targetInSights = true;
                targetDistance = aimedDistance;
                return;
            }

            if (!IsUsable(target))
                target = nearest;

            targetDistance = target != null ? SurfaceDistance(target, eye.position) : 0f;
        }

        public override void OnGUI()
        {
            DrawAsteroids();
            DrawTarget();
            DrawStats();
            DrawTests();
        }

        private void DrawAsteroids()
        {
            using (var section = new GodMenuGUI.SectionScope("Asteroids"))
            {
                if (!section.Expanded)
                    return;

                if (types.Count == 0)
                {
                    EditorGUILayout.HelpBox("No asteroid types yet. Run Set Up Sector from the Project tab outside Play Mode.", MessageType.Info);
                    return;
                }

                typeIndex = Mathf.Clamp(typeIndex, 0, types.Count - 1);
                typeIndex = EditorGUILayout.Popup("Type", typeIndex, typeNames);
                diameter = EditorGUILayout.Slider("Diameter (m)", diameter, 8f, 120f);
                distance = EditorGUILayout.Slider("Distance (m)", distance, 40f, 600f);

                switch (GodMenuGUI.ButtonRow(SpawnLabels))
                {
                    case 0:
                        Transform eye = Eye();
                        if (eye != null)
                        {
                            Vector3 position = eye.position + eye.forward * (distance + diameter * 0.5f);
                            target = system.SpawnAsteroid(types[typeIndex], diameter, (uint)Random.Range(1, int.MaxValue), position, Random.rotation);
                        }

                        break;
                    case 1:
                        system.ClearDebris();
                        if (target != null && target.IsPiece)
                            target = null;
                        break;
                    case 2:
                        foreach (VoxelBody body in system.Bodies)
                        {
                            if (body != null)
                                body.Release();
                        }

                        system.ClearDebris();
                        target = null;
                        break;
                }
            }
        }

        private void DrawTarget()
        {
            using (var section = new GodMenuGUI.SectionScope("Target Rock"))
            {
                if (!section.Expanded)
                    return;

                Transform eye = Eye();
                if (target == null || eye == null)
                {
                    GodMenuGUI.Hint("No asteroid in the scene. Spawn one above.");
                    return;
                }

                float left = target.InitialVolume > 0f ? target.Volume / target.InitialVolume : 0f;
                GodMenuGUI.Field(target.name, targetInSights ? $"in your sights, {targetDistance:N0} m" : $"{targetDistance:N0} m away");
                GodMenuGUI.Bar(left, $"{target.Volume:N0} m³ left ({left:P0})", GodMenuGUI.Accent);
                GodMenuGUI.Field("Mass", $"{target.Mass:N0} t");
                GodMenuGUI.Field("Motion", $"drifting {target.LinearVelocity.magnitude:0.00} m/s, " +
                                           $"turning {target.AngularVelocity.magnitude * Mathf.Rad2Deg:0.0}°/s");
                GodMenuGUI.Hint("Point your ship at a rock to make it the target. It stays the target until you pick another.");

                GodMenuGUI.Hint("Hit it where you aim, or on the side facing you");
                int hit = GodMenuGUI.ButtonRow(HitLabels);
                GodMenuGUI.Hint("Crack a chunk off anywhere");
                int crack = GodMenuGUI.ButtonRow(BreakLabels);
                GodMenuGUI.Hint("Move it");
                int move = GodMenuGUI.ButtonRow(MotionLabels);
                if (hit < 0 && crack < 0 && move < 0)
                    return;

                // Aim is read again at the click: the ship may have turned since the last update.
                Vector3 direction;
                Vector3 surface;
                if (InSights(eye, out Vector3 aimPoint, out _) == target)
                {
                    direction = eye.forward;
                    surface = aimPoint;
                }
                else
                {
                    direction = (target.CentreOfMass - eye.position).normalized;
                    surface = SurfacePoint(target, eye.position, direction);
                }

                switch (hit)
                {
                    case 0:
                        target.ApplyDamage(new DamageEvent(GunDamage, surface, direction, DamageKind.Weapon, CraterRadius, eye));
                        break;
                    case 1:
                        target.ApplyDamage(new DamageEvent(MissileDamage, surface, direction, DamageKind.Explosion, BlastRadius, eye));
                        break;
                    case 2:
                        system.Crumble(target);
                        break;
                }

                switch (crack)
                {
                    case 0:
                    case 1:
                    case 2:
                        target.Fracture((FractureSize)crack, DamageKind.Script);
                        break;
                    case 3:
                        target.Fracture(RandomSize(), DamageKind.Script);
                        break;
                    case 4:
                        for (int i = 0; i < ShatterCracks; ++i)
                            target.Fracture(RandomSize(), DamageKind.Script);
                        break;
                }

                switch (move)
                {
                    case 0:
                        target.AddImpulse(direction * (target.Mass * PushSpeed), surface);
                        break;
                    case 1:
                        target.SetMotion(target.LinearVelocity, Random.onUnitSphere * (SpinDegrees * Mathf.Deg2Rad));
                        break;
                    case 2:
                        target.SetMotion(Vector3.zero, Vector3.zero);
                        break;
                }
            }
        }

        private static FractureSize RandomSize()
        {
            return (FractureSize)Random.Range(0, 3);
        }

        private void DrawStats()
        {
            using (var section = new GodMenuGUI.SectionScope("Cost"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("Surface", $"{surfaceChunks} chunks, {triangles:N0} triangles");
                GodMenuGUI.Field("Detail", $"{rocks + pieces - halfDetail - quarterDetail} full, {halfDetail} half, {quarterDetail} quarter");
                GodMenuGUI.Field("Memory", $"{voxelBytes / BytesPerMegabyte:0.0} MB voxels, {system.ScratchBytes / BytesPerMegabyte:0.0} MB job buffers");
                GodMenuGUI.Field("Meshing", $"{system.MeshedLastFrame} chunks last frame, {system.QueuedChunks} queued");
                GodMenuGUI.Field("Break-ups", $"{system.PieceSearches} searches, last visited {system.LastSearchVisited:N0} voxels");
                GodMenuGUI.Field("Heavy cuts", $"{system.HeavyCuts} done on a worker thread");
                GodMenuGUI.Field("Main thread", $"{system.LastFrameMilliseconds:0.00} ms, peak {system.PeakFrameMilliseconds:0.00} ms");
                for (int stage = 0; stage < DestructionSystem.StageNames.Count; ++stage)
                {
                    GodMenuGUI.Field("  " + DestructionSystem.StageNames[stage],
                        $"{system.StageMilliseconds(stage):0.00} ms, peak {system.PeakStageMilliseconds(stage):0.00} ms");
                }

                GodMenuGUI.Field("  Trails", $"{system.TrailMilliseconds:0.00} ms");
                GodMenuGUI.Field("  Chips", $"{system.ChipMilliseconds:0.00} ms");
                GodMenuGUI.Field("Trail rocks", $"{system.TrailRockCount} drifting, {system.SolidTrailRocks} solid near you");
                GodMenuGUI.Field("Chips", $"{system.ChipCount} in the air, at most {system.ChipsPerSecond:0} a second");

                foreach (KeyValuePair<VoxelMaterialDefinition, float> removed in system.RemovedByMaterial)
                {
                    float tonnes = removed.Value * removed.Key.TonnesPerCubicMetre;
                    GodMenuGUI.Field("Dug: " + removed.Key.DisplayName, $"{removed.Value:N1} m³, {tonnes:N1} t");
                }

                if (GodMenuGUI.ButtonRow(StatsLabels) == 0)
                    system.ResetStats();
            }
        }

        private void DrawTests()
        {
            using (var section = new GodMenuGUI.SectionScope("Self-Test"))
            {
                if (!section.Expanded)
                    return;

                if (DestructionDiagnostics.LastSummary != null)
                    GodMenuGUI.Status(DestructionDiagnostics.LastPassed, "Core: " + DestructionDiagnostics.LastSummary);
                if (DestructionSmokeTest.LastSummary != null)
                    GodMenuGUI.Status(DestructionSmokeTest.IsRunning || DestructionSmokeTest.LastPassed, "Smoke: " + DestructionSmokeTest.LastSummary);
                if (DestructionBenchmark.LastSummary != null)
                    GodMenuGUI.Field("Benchmark", DestructionBenchmark.LastSummary);

                bool busy = DestructionSmokeTest.IsRunning || DestructionBenchmark.IsRunning;
                bool[] enabled = { true, !busy && types.Count > 0, !busy && types.Count > 0 };
                switch (GodMenuGUI.ButtonRow(TestLabels, enabled))
                {
                    case 0:
                        DestructionDiagnostics.Run();
                        break;
                    case 1:
                        DestructionDiagnostics.RunSmokeTest();
                        break;
                    case 2:
                        DestructionBenchmark.Begin(types[Mathf.Clamp(typeIndex, 0, types.Count - 1)]);
                        break;
                }

                GodMenuGUI.Hint("Benchmark: nine rocks of the type above under 800 hits a second for ten seconds; results in the Console.");
            }
        }

        private void RefreshTypes()
        {
            types.Clear();
            types.AddRange(DestructionSetup.FindAsteroidTypes());
            typeNames = new string[types.Count];
            for (int i = 0; i < types.Count; ++i)
                typeNames[i] = types[i].DisplayName;
        }

        // The player's ship when flying, otherwise the camera.
        private static Transform Eye()
        {
            Vehicle vehicle = GodMenuContext.PlayerVehicle;
            if (vehicle != null)
                return vehicle.transform;

            Camera camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        // The rock straight ahead of the eye, with where and how far away the line of sight meets it.
        private static VoxelBody InSights(Transform eye, out Vector3 point, out float distance)
        {
            point = Vector3.zero;
            distance = float.MaxValue;
            VoxelBody best = null;
            int count = Physics.RaycastNonAlloc(eye.position, eye.forward, aimHits, AimRange, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = aimHits[i];
                var receiver = hit.collider.GetComponent<DestructionDamageReceiver>();
                if (receiver == null || !(receiver.Target is VoxelBody body) || !IsUsable(body) || hit.distance >= distance)
                    continue;

                best = body;
                point = hit.point;
                distance = hit.distance;
            }

            return best;
        }

        private static bool IsUsable(VoxelBody body)
        {
            return body != null && body.IsAllocated && !body.IsReleased && !body.IsWarmUp;
        }

        // Distance to the rock's surface, roughly: its centre less the radius of a ball of the same volume. Measuring to
        // centres would favour small pieces, whose centres sit nearer their surface.
        private static float SurfaceDistance(VoxelBody body, Vector3 from)
        {
            float radius = Mathf.Pow(Mathf.Max(body.Volume, 0f) * (3f / (4f * Mathf.PI)), 1f / 3f);
            return Mathf.Max(0f, Vector3.Distance(from, body.CentreOfMass) - radius);
        }

        // Where a ray from the viewer meets the rock, or its centre when the ray misses.
        private static Vector3 SurfacePoint(VoxelBody body, Vector3 from, Vector3 direction)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, direction, Vector3.Distance(from, body.transform.position) + 500f);
            float best = float.MaxValue;
            Vector3 point = body.transform.position;
            foreach (RaycastHit hit in hits)
            {
                var receiver = hit.collider.GetComponent<DestructionDamageReceiver>();
                if (receiver != null && ReferenceEquals(receiver.Target, body) && hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }

            return point;
        }
    }
}
