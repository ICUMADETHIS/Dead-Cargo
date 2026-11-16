using System.Collections;
using System.Collections.Generic;
using System.Text;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using UnityEngine;

namespace DeadCargo.DevTesting
{
    /// <summary>
    /// Drives the whole destruction pipeline in Play Mode the way the game does, and reports to the Console: builds an
    /// ore asteroid in view, hits it through the kit's weapon receiver, checks the crater is solid to physics, digs to
    /// ore, saws it in two and follows the piece, and digs a small rock out entirely. Started from the God Menu
    /// Destruction tab. The test asteroid is left in place to look at.
    /// </summary>
    public sealed class DestructionSmokeTest : MonoBehaviour
    {
        private const string Title = "Destruction smoke test";
        private const float BuildTimeout = 20f;
        private const float StepTimeout = 5f;
        private const float RockDiameter = 36f;
        private const float ViewDistance = 110f;

        private readonly StringBuilder report = new StringBuilder();
        private readonly List<string> failures = new List<string>();
        private AsteroidDefinition oreType;
        private AsteroidDefinition smallType;
        private int checks;

        public static bool IsRunning { get; private set; }

        /// <summary>One line describing the last run, or null before the first.</summary>
        public static string LastSummary { get; private set; }

        public static bool LastPassed { get; private set; }

        /// <summary>Starts a run. Returns false when one is already running or Play Mode is off.</summary>
        public static bool Begin(AsteroidDefinition oreType, AsteroidDefinition smallType)
        {
            if (IsRunning || !Application.isPlaying || oreType == null)
                return false;

            var test = new GameObject(Title).AddComponent<DestructionSmokeTest>();
            test.oreType = oreType;
            test.smallType = smallType != null ? smallType : oreType;
            IsRunning = true;
            LastSummary = "Running...";
            return true;
        }

        private IEnumerator Start()
        {
            yield return null;

            DestructionSystem system = DestructionSystem.Active;
            if (system == null)
            {
                system = new GameObject("Destruction System (Smoke Test)").AddComponent<DestructionSystem>();
                Note("Scene", "no Destruction System in the scene, so a temporary one was added; run Set Up Sector to add one for good");
            }

            system.ResetStats();
            Camera view = Camera.main;
            Transform eye = view != null ? view.transform : transform;
            Vector3 forward = eye.forward;
            Vector3 centre = eye.position + forward * ViewDistance;

            VoxelBody rock = system.SpawnAsteroid(oreType, RockDiameter, 20261116u, centre, Quaternion.identity);
            float started = Time.realtimeSinceStartup;
            while (rock != null && !rock.IsReady && Time.realtimeSinceStartup - started < BuildTimeout)
                yield return null;

            float buildSeconds = Time.realtimeSinceStartup - started;
            bool built = rock != null && rock.IsReady;
            Check(built, "Asteroid builds", built
                ? $"'{oreType.DisplayName}', {rock.SurfaceChunks} chunks, {rock.TriangleCount:N0} triangles, {rock.Volume:N0} m³ in {buildSeconds:0.00} s"
                : $"not ready after {BuildTimeout:0} s");
            if (!built)
            {
                Finish();
                yield break;
            }

            float sphere = 4f / 3f * Mathf.PI * Mathf.Pow(RockDiameter * 0.5f, 3f);
            Check(rock.Volume > sphere * 0.3f && rock.Volume < sphere * 3f, "Asteroid has a sensible size", $"{rock.Volume:N0} m³ against {sphere:N0} m³ for a plain sphere");

            yield return WaitForColliders(rock);
            int colliding = 0;
            foreach (VoxelChunk chunk in rock.Chunks)
            {
                if (chunk != null && chunk.Triangles > 0 && chunk.Collider.enabled && chunk.Collider.sharedMesh != null)
                    colliding++;
            }

            Check(colliding == rock.SurfaceChunks, "Every surface chunk has a collider", $"{colliding} of {rock.SurfaceChunks}");

            // A ray from the viewer, like a gun's: it must reach the rock's weapon receiver.
            Vector3 from = centre - forward * (RockDiameter * 2f);
            bool hit = Physics.Raycast(from, forward, out RaycastHit surface, RockDiameter * 4f);
            var receiver = hit ? surface.collider.GetComponent<DestructionDamageReceiver>() : null;
            Check(receiver != null && ReferenceEquals(receiver.Target, rock), "Weapons can hit it",
                hit ? $"ray hit '{surface.collider.name}' {surface.distance:0.0} m out" : "ray missed");

            if (receiver != null)
            {
                int cuts = 0;
                float cutVolume = 0f;
                rock.MaterialRemoved += cut => { cuts++; cutVolume += cut.Total; };

                float before = rock.Volume;
                receiver.Damage(400f, surface.point, null, eye);
                float hitTime = Time.realtimeSinceStartup;
                while (cuts == 0 && Time.realtimeSinceStartup - hitTime < StepTimeout)
                    yield return null;

                yield return WaitFrames(1);
                yield return WaitForColliders(rock);

                Check(cuts == 1 && cutVolume > 0.5f, "A weapon hit carves a crater", $"{cuts} cut, {cutVolume:0.00} m³ removed");
                Check(Mathf.Abs(before - rock.Volume - cutVolume) < Mathf.Max(0.01f, cutVolume * 0.001f), "Removed rock is accounted for",
                    $"volume fell by {before - rock.Volume:0.000} m³, reported {cutVolume:0.000} m³");

                bool again = Physics.Raycast(from, forward, out RaycastHit crater, RockDiameter * 4f);
                float deeper = again ? crater.distance - surface.distance : 0f;
                Check(again && deeper > 0.2f, "The crater is solid to physics", again ? $"surface moved back {deeper:0.00} m" : "ray missed");
            }

            float ore = 0f;
            float dug = 0f;
            rock.MaterialRemoved += cut =>
            {
                dug += cut.Total;
                for (int slot = 1; slot < VoxelEncoding.MaterialSlots; ++slot)
                    ore += cut[slot];
            };

            rock.Carve(centre, 8f, DamageKind.Mining);
            started = Time.realtimeSinceStartup;
            while (dug <= 0f && Time.realtimeSinceStartup - started < StepTimeout)
                yield return null;
            Check(dug > 0f && ore > 0f, "Digging reaches ore, reported by material", $"{dug:0.0} m³ dug, {ore:0.0} m³ of it ore");

            VoxelBody piece = null;
            void OnSplit(VoxelBody body, VoxelBody newPiece)
            {
                if (body == rock && piece == null)
                    piece = newPiece;
            }

            system.BodySplit += OnSplit;
            float volumeBefore = rock.Volume;
            float sawn = 0f;
            rock.MaterialRemoved += cut => sawn += cut.Total;
            Vector3 side = eye.right;
            rock.Cut(centre + side * (RockDiameter * 0.15f), side, 1.5f, RockDiameter * 2f, DamageKind.Script);

            started = Time.realtimeSinceStartup;
            while (piece == null && Time.realtimeSinceStartup - started < StepTimeout)
                yield return null;

            system.BodySplit -= OnSplit;
            Check(piece != null, "Sawing through splits the rock", piece != null ? $"split after {Time.realtimeSinceStartup - started:0.00} s" : "no piece broke away");

            if (piece != null)
            {
                started = Time.realtimeSinceStartup;
                while (piece != null && !piece.IsReady && Time.realtimeSinceStartup - started < StepTimeout)
                    yield return null;

                bool ready = piece != null && piece.IsReady;
                Check(ready && piece.IsPiece && piece.PhysicsBody != null && piece.PhysicsBody.isKinematic, "The piece drifts free",
                    ready ? $"{piece.Volume:N0} m³, {piece.SurfaceChunks} chunks" : "piece never finished building");

                if (ready)
                {
                    float kept = rock.Volume + piece.Volume;
                    float expected = volumeBefore - sawn;
                    Check(Mathf.Abs(kept - expected) < expected * 0.02f, "No rock is lost or doubled by the split",
                        $"rock {rock.Volume:N0} + piece {piece.Volume:N0} = {kept:N0} m³, expected {expected:N0} m³");

                    Vector3 position = piece.transform.position;
                    float waitStart = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitStart < 0.6f)
                        yield return null;

                    float moved = piece != null ? Vector3.Distance(position, piece.transform.position) : 0f;
                    Check(moved > 0.2f, "The piece moves away", $"{moved:0.00} m in 0.6 s");
                }
            }

            VoxelBody pebble = system.SpawnAsteroid(smallType, 8f, 7u, centre + eye.up * (RockDiameter * 1.5f), Quaternion.identity);
            started = Time.realtimeSinceStartup;
            while (pebble != null && !pebble.IsReady && Time.realtimeSinceStartup - started < BuildTimeout)
                yield return null;

            if (pebble != null && pebble.IsReady)
            {
                bool depleted = false;
                pebble.Depleted += _ => depleted = true;
                pebble.Carve(pebble.transform.position, 30f, DamageKind.Script);

                started = Time.realtimeSinceStartup;
                while (!depleted && Time.realtimeSinceStartup - started < StepTimeout)
                    yield return null;

                yield return WaitFrames(3);
                Check(depleted && pebble == null, "A rock dug out entirely is removed", depleted ? "depleted and released" : "never reported empty");
            }
            else
            {
                Check(false, "A rock dug out entirely is removed", "the small rock never finished building");
            }

            Note("Main thread", $"destruction work peaked at {system.PeakFrameMilliseconds:0.00} ms in a frame during the test " +
                                "(editor, with safety checks on; builds are faster)");
            Note("Memory", $"{(system.ScratchBytes / (1024f * 1024f)):0.0} MB of shared job buffers");
            Finish();
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; ++i)
                yield return null;
        }

        // Colliders are cooked off the main thread and switch over a frame or more after the mesh.
        private static IEnumerator WaitForColliders(VoxelBody body)
        {
            float started = Time.realtimeSinceStartup;
            while (body != null && !CollidersCurrent(body) && Time.realtimeSinceStartup - started < StepTimeout)
                yield return null;
        }

        private static bool CollidersCurrent(VoxelBody body)
        {
            if (body.PendingMeshes > 0)
                return false;

            foreach (VoxelChunk chunk in body.Chunks)
            {
                if (chunk != null && chunk.Triangles > 0 && (!chunk.Collider.enabled || chunk.ColliderMesh != chunk.RenderMesh))
                    return false;
            }

            return true;
        }

        private void Check(bool passed, string name, string detail)
        {
            ++checks;
            report.Append(passed ? "  pass  " : "  FAIL  ").Append(name).Append("  ·  ").AppendLine(detail);
            if (!passed)
                failures.Add(name);
        }

        private void Note(string name, string detail)
        {
            report.Append("  info  ").Append(name).Append("  ·  ").AppendLine(detail);
        }

        private void Finish()
        {
            LastPassed = failures.Count == 0;
            LastSummary = LastPassed ? $"{checks} checks passed" : $"{failures.Count} of {checks} checks failed";
            string text = $"{Title}: {LastSummary}.\n{report}";
            if (LastPassed)
                Debug.Log(text);
            else
                Debug.LogError(text);

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            IsRunning = false;
            if (LastSummary == "Running...")
                LastSummary = "Stopped before it finished";
        }
    }
}
