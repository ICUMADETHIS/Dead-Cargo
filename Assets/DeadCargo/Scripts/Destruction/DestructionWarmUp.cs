using System.Collections.Generic;
using DeadCargo.Destruction.Voxels;
using UnityEngine;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Runs the whole destruction pipeline once, out of sight, when a scene builds its first asteroid. Code running for
    /// the first time pays to start (compiling, job set-up, first objects), which made the player's first hit and first
    /// break the slowest frames of a run. A small hidden rock is built, cratered, cracked in two and freed, while the
    /// mesh and piece pools fill a little each frame and the cross-fade material is made. It takes about half a second,
    /// under the load of the scene's own asteroids, and raises no system events.
    /// </summary>
    internal sealed class DestructionWarmUp
    {
        private enum Step
        {
            Waiting,
            Building,
            Breaking,
            Settling,
            Done
        }

        // Far below the play space: past every camera's far clip and away from all sector content.
        private static readonly Vector3 Position = new Vector3(0f, -50000f, 0f);

        private const string Name = "Destruction Warm-Up";
        private const float Diameter = 12f;
        private const uint Seed = 0x5EED5u;
        private const float CraterRadius = 1.5f;
        private const float CrackShare = 0.35f;
        private const float StepTimeout = 3f;

        // Frames the freed pieces settle for, so their colliders are cooked and switched in too.
        private const int SettleFrames = 4;

        // Pieces made ready in the pool, one a frame, each with spare chunk objects for its first breaks.
        private const int PrewarmPieces = 4;
        private const int PrewarmChunksPerPiece = 16;

        private readonly DestructionSystem system;
        private readonly List<VoxelBody> pieces = new List<VoxelBody>();
        private VoxelBody rock;
        private Material material;
        private Step step;
        private float stepStarted;
        private int settled;
        private int prewarmed;

        public DestructionWarmUp(DestructionSystem system)
        {
            this.system = system;
        }

        public bool HasStarted => step != Step.Waiting;

        /// <summary>True from the start until the rock is gone and the pools are full.</summary>
        public bool IsRunning => HasStarted && (step != Step.Done || prewarmed < PrewarmPieces);

        public void Begin(AsteroidDefinition definition)
        {
            if (HasStarted || definition == null)
                return;

            // Set first: building the rock goes through the same path that starts the warm-up.
            Enter(Step.Building);
            material = definition.SurfaceMaterial;
            system.PrewarmRendering(material);

            var holder = new GameObject(Name) { hideFlags = HideFlags.HideInHierarchy };
            holder.transform.SetPositionAndRotation(Position, Quaternion.identity);
            rock = holder.AddComponent<VoxelBody>();
            system.Generate(rock, definition, Diameter, Seed);
            rock.MarkAsWarmUp();
            rock.Split += OnSplit;
        }

        /// <summary>Moves the warm-up on. Called once a frame, before the scheduler runs.</summary>
        public void Update()
        {
            if (!IsRunning)
                return;

            if (prewarmed < PrewarmPieces)
            {
                system.PrewarmPiece(PrewarmChunksPerPiece, material);
                prewarmed++;
            }

            bool timedOut = Time.unscaledTime - stepStarted > StepTimeout;
            switch (step)
            {
                case Step.Building:
                    if (rock == null || rock.IsReleased || timedOut)
                        Finish();
                    else if (rock.IsReady)
                        Break();
                    break;

                case Step.Breaking:
                    if (rock == null || timedOut)
                        Finish();
                    else if (pieces.Count > 0 && rock.IsReady && PiecesReady())
                        Enter(Step.Settling);
                    break;

                case Step.Settling:
                    if (++settled >= SettleFrames)
                        Finish();
                    break;
            }
        }

        // A crater cut inline and a crack cut on a worker, which breaks a piece away: every path a weapon hit takes.
        private void Break()
        {
            Vector3 top = rock.CentreOfMass + Vector3.up * (rock.EquivalentDiameter * 0.45f);
            rock.ApplyDamage(new DamageEvent(1f, top, Vector3.down, DamageKind.Script, CraterRadius));
            rock.Fracture(Vector3.right, CrackShare, DamageKind.Script);
            Enter(Step.Breaking);
        }

        private bool PiecesReady()
        {
            foreach (VoxelBody piece in pieces)
            {
                if (piece != null && !piece.IsReleased && !piece.IsReady)
                    return false;
            }

            return true;
        }

        private void OnSplit(VoxelBody body, VoxelBody piece)
        {
            pieces.Add(piece);
        }

        private void Finish()
        {
            if (rock != null)
            {
                rock.Split -= OnSplit;
                rock.Release();
            }

            foreach (VoxelBody piece in pieces)
            {
                if (piece != null && piece.IsWarmUp)
                    piece.Release();
            }

            pieces.Clear();
            rock = null;
            Enter(Step.Done);
        }

        private void Enter(Step next)
        {
            step = next;
            stepStarted = Time.unscaledTime;
        }
    }
}
