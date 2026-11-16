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
    /// <summary>Spawn voxel asteroids, damage them, watch the destruction pipeline's cost, and run its tests.</summary>
    internal sealed class DestructionTab : GodMenuTab
    {
        private const float CraterRadius = 2.5f;
        private const float BlastRadius = 7f;
        private const float CutThickness = 1.5f;
        private const float BytesPerMegabyte = 1024f * 1024f;

        private static readonly string[] SpawnLabels = { "Spawn Ahead", "Clear Pieces", "Remove All" };
        private static readonly string[] DamageLabels = { "Crater", "Blast", "Cut In Two", "Crumble" };
        private static readonly string[] TestLabels = { "Core Self-Test", "Smoke Test" };
        private static readonly string[] StatsLabels = { "Reset Stats" };

        private readonly List<AsteroidDefinition> types = new List<AsteroidDefinition>();
        private string[] typeNames = new string[0];
        private int typeIndex;
        private float diameter = 40f;
        private float distance = 120f;

        private DestructionSystem system;
        private VoxelBody nearest;
        private float nearestDistance;
        private int rocks;
        private int pieces;
        private int surfaceChunks;
        private int triangles;
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
            nearest = null;
            rocks = pieces = surfaceChunks = triangles = 0;
            voxelBytes = 0;
            if (system == null)
                return;

            if (types.Count == 0)
                RefreshTypes();

            Transform eye = Eye();
            nearestDistance = float.MaxValue;
            foreach (VoxelBody body in system.Bodies)
            {
                if (body == null || !body.IsAllocated)
                    continue;

                if (body.IsFree)
                    pieces++;
                else
                    rocks++;

                surfaceChunks += body.SurfaceChunks;
                triangles += body.TriangleCount;
                voxelBytes += body.MemoryBytes;

                if (eye == null || !body.IsReady)
                    continue;

                float d = Vector3.Distance(eye.position, body.transform.position);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = body;
                }
            }
        }

        public override void OnGUI()
        {
            DrawAsteroids();
            DrawDamage();
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
                            system.SpawnAsteroid(types[typeIndex], diameter, (uint)Random.Range(1, int.MaxValue), position, Random.rotation);
                        }

                        break;
                    case 1:
                        system.ClearDebris();
                        break;
                    case 2:
                        foreach (VoxelBody body in system.Bodies)
                        {
                            if (body != null)
                                body.Release();
                        }

                        system.ClearDebris();
                        break;
                }
            }
        }

        private void DrawDamage()
        {
            using (var section = new GodMenuGUI.SectionScope("Damage Nearest"))
            {
                if (!section.Expanded)
                    return;

                if (nearest == null)
                {
                    GodMenuGUI.Hint("No asteroid in the scene. Spawn one above.");
                    return;
                }

                float left = nearest.InitialVolume > 0f ? nearest.Volume / nearest.InitialVolume : 0f;
                GodMenuGUI.Field(nearest.name, $"{nearestDistance:N0} m away");
                GodMenuGUI.Bar(left, $"{nearest.Volume:N0} m³ left ({left:P0})", GodMenuGUI.Accent);

                int clicked = GodMenuGUI.ButtonRow(DamageLabels);
                if (clicked < 0)
                    return;

                Transform eye = Eye();
                Vector3 toRock = (nearest.transform.position - eye.position).normalized;
                Vector3 surface = SurfacePoint(nearest, eye.position, toRock);
                switch (clicked)
                {
                    case 0:
                        nearest.Carve(surface + toRock * (CraterRadius * 0.3f), CraterRadius, DamageKind.Script, -toRock);
                        break;
                    case 1:
                        nearest.Carve(surface + toRock * (BlastRadius * 0.3f), BlastRadius, DamageKind.Explosion, -toRock);
                        break;
                    case 2:
                        float reach = Mathf.Pow(nearest.Volume, 1f / 3f) * 2f + 10f;
                        nearest.Cut(nearest.transform.position, eye.right, CutThickness, reach, DamageKind.Script);
                        break;
                    case 3:
                        system.Crumble(nearest);
                        break;
                }
            }
        }

        private void DrawStats()
        {
            using (var section = new GodMenuGUI.SectionScope("Cost"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("Surface", $"{surfaceChunks} chunks, {triangles:N0} triangles");
                GodMenuGUI.Field("Memory", $"{voxelBytes / BytesPerMegabyte:0.0} MB voxels, {system.ScratchBytes / BytesPerMegabyte:0.0} MB job buffers");
                GodMenuGUI.Field("Meshing", $"{system.MeshedLastFrame} chunks last frame, {system.QueuedChunks} queued");
                GodMenuGUI.Field("Break-ups", $"{system.PieceSearches} searches, last visited {system.LastSearchVisited:N0} voxels");
                GodMenuGUI.Field("Main thread", $"{system.LastFrameMilliseconds:0.00} ms, peak {system.PeakFrameMilliseconds:0.00} ms");
                for (int stage = 0; stage < DestructionSystem.StageNames.Count; ++stage)
                {
                    GodMenuGUI.Field("  " + DestructionSystem.StageNames[stage],
                        $"{system.StageMilliseconds(stage):0.00} ms, peak {system.PeakStageMilliseconds(stage):0.00} ms");
                }

                GodMenuGUI.Field("Chips", $"{system.ChipCount} in the air");

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

                bool[] enabled = { true, !DestructionSmokeTest.IsRunning && types.Count > 0 };
                switch (GodMenuGUI.ButtonRow(TestLabels, enabled))
                {
                    case 0:
                        DestructionDiagnostics.Run();
                        break;
                    case 1:
                        DestructionDiagnostics.RunSmokeTest();
                        break;
                }
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
