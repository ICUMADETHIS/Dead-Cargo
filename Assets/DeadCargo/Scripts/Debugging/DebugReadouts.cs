#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System.Collections.Generic;
using System.Text;
using DeadCargo.Authority;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using DeadCargo.Extraction;
using DeadCargo.Flow;
using DeadCargo.Heat;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Field;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// Writes each channel's panel text. Reads only public game API, never changes anything, and allocates nothing
    /// beyond the strings the logged messages already are.
    /// </summary>
    internal static class DebugReadouts
    {
        private const int MaxShipRows = 8;
        private const string Missing = "n/a";

        private static readonly DebugLog.Entry[] logEntries = new DebugLog.Entry[DebugLog.Capacity];
        private static string[] qualityNames;

        /// <summary>The always-on line: frame rate, frame time, and a count of errors when there are any.</summary>
        public static void WriteCounter(StringBuilder text, DebugContext context, bool showHint)
        {
            DebugFrameStats stats = context.Stats;
            text.FrameColour(stats.AverageMs).Append("<b>").Number(stats.Fps).Append(" FPS</b>").Append(DebugText.End)
                .Append("  ").Number(stats.AverageMs, 1).Append(" ms");

            int errors = DebugLog.Errors;
            if (errors > 0)
                text.Append("  ").Append(DebugText.Bad).Append("! ").Integer(errors).Append(errors == 1 ? " error" : " errors").Append(DebugText.End);

            if (showHint)
                text.Append('\n').Append(DebugText.Muted).Append("F3 hide   F4 channels").Append(DebugText.End);
        }

        public static void Write(DebugChannel channel, StringBuilder text, DebugContext context)
        {
            switch (channel)
            {
                case DebugChannel.Performance:
                    WritePerformance(text, context);
                    break;
                case DebugChannel.Rendering:
                    WriteRendering(text, context.Stats);
                    break;
                case DebugChannel.Memory:
                    WriteMemory(text, context.Stats);
                    break;
                case DebugChannel.Physics:
                    WritePhysics(text, context);
                    break;
                case DebugChannel.Camera:
                    WriteCamera(text, context);
                    break;
                case DebugChannel.Player:
                    WritePlayer(text, context);
                    break;
                case DebugChannel.Gameplay:
                    WriteGameplay(text, context);
                    break;
                case DebugChannel.Ai:
                    WriteAi(text, context);
                    break;
                case DebugChannel.Sector:
                    WriteSector(text, context);
                    break;
                case DebugChannel.Destruction:
                    WriteDestruction(text);
                    break;
                case DebugChannel.Log:
                    WriteLog(text);
                    break;
            }
        }

        /// <summary>The channel picker: every channel with its key and state.</summary>
        public static void WritePicker(StringBuilder text)
        {
            text.Heading("DEBUG CHANNELS");
            for (int i = 0; i < DebugChannels.Count; ++i)
            {
                var channel = (DebugChannel)i;
                bool on = DebugSettings.IsOn(channel);
                text.Append('\n').Append(DebugText.Muted).Append('[').Append(DebugChannels.PickerKeyLabel(channel)).Append("] ").Append(DebugText.End)
                    .Append(on ? DebugText.Good : DebugText.Muted).Append(on ? "ON  " : "off ").Append(DebugChannels.Name(channel)).Append(DebugText.End);
            }

            text.Append('\n').Append(DebugText.Muted).Append("keys switch channels   F4 close").Append(DebugText.End);
        }

        private static void WritePerformance(StringBuilder text, DebugContext context)
        {
            DebugFrameStats stats = context.Stats;
            text.Heading("PERFORMANCE");
            text.Row("frame rate").FrameColour(stats.AverageMs).Number(stats.Fps, 1).Append(DebugText.End)
                .Append(DebugText.Muted).Append("  1% low ").Append(DebugText.End)
                .FrameColour(1000f / Mathf.Max(stats.OnePercentLowFps, 1f)).Number(stats.OnePercentLowFps).Append(DebugText.End);
            text.Row("frame").Number(stats.AverageMs, 2).Append(" ms").Append(DebugText.Muted).Append("  min ").Append(DebugText.End)
                .Number(stats.MinMs, 1).Append(DebugText.Muted).Append("  max ").Append(DebugText.End).FrameColour(stats.MaxMs)
                .Number(stats.MaxMs, 1).Append(DebugText.End);

            text.Row("cpu main");
            Milliseconds(text, stats.MainThreadMs);
            text.Append(DebugText.Muted).Append("  render ").Append(DebugText.End);
            Milliseconds(text, stats.RenderThreadMs);
            text.Row("gpu");
            Milliseconds(text, stats.GpuMs);

            DestructionSystem destruction = DestructionSystem.Active;
            if (destruction != null)
                text.Row("destruction").Number(destruction.LastFrameMilliseconds, 2).Append(" ms").Append(DebugText.Muted).Append("  peak ")
                    .Append(DebugText.End).Number(destruction.PeakFrameMilliseconds, 2);

            text.Row("overlay").Number(context.OverlayMs, 2).Append(" ms");
        }

        private static void WriteRendering(StringBuilder text, DebugFrameStats stats)
        {
            text.Heading("RENDERING");
            text.Row("batches");
            Counter(text, stats, DebugCounter.Batches);
            text.Append(DebugText.Muted).Append("  setpass ").Append(DebugText.End);
            Counter(text, stats, DebugCounter.SetPassCalls);
            text.Row("draw calls");
            Counter(text, stats, DebugCounter.DrawCalls);
            text.Append(DebugText.Muted).Append("  shadows ").Append(DebugText.End);
            Counter(text, stats, DebugCounter.ShadowCasters);
            text.Row("triangles");
            Counter(text, stats, DebugCounter.Triangles);
            text.Row("vertices");
            Counter(text, stats, DebugCounter.Vertices);

            text.Row("screen").Integer(Screen.width, false).Append(" x ").Integer(Screen.height, false);
            qualityNames ??= QualitySettings.names;
            int level = QualitySettings.GetQualityLevel();
            text.Row("quality").Append(level >= 0 && level < qualityNames.Length ? qualityNames[level] : Missing)
                .Append(DebugText.Muted).Append("  vsync ").Append(DebugText.End).Integer(QualitySettings.vSyncCount);
            if (Application.targetFrameRate > 0)
                text.Append(DebugText.Muted).Append("  cap ").Append(DebugText.End).Integer(Application.targetFrameRate);
        }

        private static void WriteMemory(StringBuilder text, DebugFrameStats stats)
        {
            text.Heading("MEMORY");
            text.Row("total");
            BytesCounter(text, stats, DebugCounter.TotalUsedMemory);
            text.Append(DebugText.Muted).Append(" of ").Append(DebugText.End);
            BytesCounter(text, stats, DebugCounter.TotalReservedMemory);
            text.Row("managed");
            BytesCounter(text, stats, DebugCounter.ManagedUsedMemory);
            text.Append(DebugText.Muted).Append(" of ").Append(DebugText.End);
            BytesCounter(text, stats, DebugCounter.ManagedReservedMemory);
            text.Row("graphics");
            BytesCounter(text, stats, DebugCounter.GraphicsMemory);
            text.Row("system");
            BytesCounter(text, stats, DebugCounter.SystemMemory);

            // Garbage made every frame is the usual source of collection hitches, so any at all shows as a warning.
            text.Row("garbage");
            if (stats.Has(DebugCounter.ManagedAllocatedInFrame))
            {
                long bytes = stats.Value(DebugCounter.ManagedAllocatedInFrame);
                text.Append(bytes == 0 ? DebugText.Good : DebugText.Warn).Bytes(bytes).Append(" a frame").Append(DebugText.End);
                if (stats.Has(DebugCounter.ManagedAllocationsInFrame))
                    text.Append(DebugText.Muted).Append(" in ").Append(DebugText.End).Integer(stats.Value(DebugCounter.ManagedAllocationsInFrame));
            }
            else
            {
                text.Append(Missing);
            }

            text.Row("collections").Integer(System.GC.CollectionCount(0));
        }

        private static void WritePhysics(StringBuilder text, DebugContext context)
        {
            DebugFrameStats stats = context.Stats;
            text.Heading("PHYSICS");
            text.Row("dynamic");
            Counter(text, stats, DebugCounter.ActiveDynamicBodies);
            text.Append(DebugText.Muted).Append(" awake of ").Append(DebugText.End);
            Counter(text, stats, DebugCounter.DynamicBodies);
            text.Row("kinematic");
            Counter(text, stats, DebugCounter.ActiveKinematicBodies);
            text.Append(DebugText.Muted).Append(" awake").Append(DebugText.End);
            text.Row("contacts");
            Counter(text, stats, DebugCounter.Contacts);
            text.Row("step").Number(Time.fixedDeltaTime * 1000f, 1).Append(" ms").Append(DebugText.Muted).Append("  ").Append(DebugText.End)
                .Number(1f / Mathf.Max(Time.fixedDeltaTime, 1e-4f)).Append(" Hz");
            text.Row("near camera").Integer(context.NearbyBodies).Append(" bodies");
            text.Append('\n').Append(DebugText.Muted).Append("green awake  grey asleep  blue kinematic").Append(DebugText.End);
        }

        private static void WriteCamera(StringBuilder text, DebugContext context)
        {
            text.Heading("CAMERA");
            Camera camera = context.Camera;
            if (camera == null)
            {
                text.Append('\n').Append(DebugText.Muted).Append("no main camera").Append(DebugText.End);
                return;
            }

            Transform view = camera.transform;
            Vector3 angles = view.eulerAngles;
            text.Row("position").Position(view.position);
            text.Row("facing").Number(angles.x).Append(", ").Number(angles.y).Append(", ").Number(angles.z);
            text.Row("speed").Number(context.CameraVelocity.magnitude, 1).Append(" m/s");
            text.Row("view").Number(camera.fieldOfView, 1).Append(" deg").Append(DebugText.Muted).Append("  clip ").Append(DebugText.End)
                .Number(camera.nearClipPlane, 2).Append(" - ").Number(camera.farClipPlane).Append(" m");
        }

        private static void WritePlayer(StringBuilder text, DebugContext context)
        {
            text.Heading("PLAYER");
            Vehicle ship = context.Player;
            if (ship == null)
            {
                text.Append('\n').Append(DebugText.Muted).Append("not flying a ship").Append(DebugText.End);
                return;
            }

            Rigidbody body = ship.CachedRigidbody;
            text.Append(DebugText.Muted).Append("  ").Append(ship.Label).Append(DebugText.End);
            text.Row("speed").Number(body != null ? body.linearVelocity.magnitude : 0f, 1).Append(" m/s");
            if (body != null)
                text.Append(DebugText.Muted).Append("  mass ").Append(DebugText.End).Number(body.mass, 1);
            text.Row("position").Position(ship.transform.position);

            Health health = context.PlayerHealth;
            if (health != null)
            {
                foreach (Damageable damageable in health.Damageables)
                {
                    if (damageable == null || damageable.HealthType == null)
                        continue;

                    float share = damageable.HealthCapacity > 0f ? damageable.CurrentHealth / damageable.HealthCapacity : 0f;
                    text.Row(damageable.HealthType.name).LoadColour(1f - share).Number(damageable.CurrentHealth).Append(DebugText.End)
                        .Append(DebugText.Muted).Append(" / ").Append(DebugText.End).Number(damageable.HealthCapacity);
                }
            }

            var target = context.PlayerTargets != null ? context.PlayerTargets.SelectedTarget : null;
            text.Row("target");
            if (target != null)
                text.Append(target.Label).Append(DebugText.Muted).Append("  ").Append(DebugText.End)
                    .Number(Vector3.Distance(ship.transform.position, target.transform.position)).Append(" m");
            else
                text.Append(DebugText.Muted).Append("none").Append(DebugText.End);

            if (context.PlayerHold != null)
                text.Row("cargo").LoadColour(context.PlayerHold.LoadFraction).Number(context.PlayerHold.TotalMass, 1).Append(DebugText.End)
                    .Append(DebugText.Muted).Append(" / ").Append(DebugText.End).Number(context.PlayerHold.MassCapacity, 1).Append(" t");
        }

        private static void WriteGameplay(StringBuilder text, DebugContext context)
        {
            text.Heading("GAMEPLAY").Append(DebugText.Muted).Append("  ").Append(context.SceneName).Append(DebugText.End);
            if (SceneFlow.IsLoading)
                text.Row("scene").Append(DebugText.Warn).Append("loading").Append(DebugText.End);

            RunTracker run = RunTracker.Active;
            if (run != null)
            {
                text.Row("run").Clock(run.Elapsed);
                if (run.IsOver)
                    text.Append(DebugText.Info).Append("  over: ").Append(OutcomeName(run.Result.Outcome)).Append(DebugText.End);

                JumpDrive drive = run.Drive;
                if (drive != null)
                {
                    text.Row("jump drive");
                    if (drive.HasJumped)
                        text.Append(DebugText.Info).Append("jumped").Append(DebugText.End);
                    else if (drive.IsSpooling)
                        text.Append(DebugText.Warn).Append("spooling ").Number(drive.SpoolRemaining, 1).Append(" s").Append(DebugText.End)
                            .Append(drive.IsHeld ? "  held" : string.Empty);
                    else
                        text.Append(drive.CanSpool ? DebugText.Good : DebugText.Muted).Append(drive.CanSpool ? "ready" : "idle").Append(DebugText.End);

                    text.Append(DebugText.Muted).Append("  fuel ").Append(DebugText.End).Integer(drive.DarkMatterAboard)
                        .Append(DebugText.Muted).Append(" / ").Append(DebugText.End).Integer(drive.DarkMatterPerJump);
                }
            }

            HeatTracker heat = HeatTracker.Active;
            if (heat != null)
                text.Row("heat").LoadColour(heat.NormalizedHeat).Number(heat.Heat).Append(DebugText.End).Append(DebugText.Muted).Append(" / ")
                    .Append(DebugText.End).Number(heat.MaxHeat).Append("  ").Append(heat.CurrentTier.Label);

            if (context.PlayerHold != null)
                text.Row("cargo").Number(context.PlayerHold.TotalMass, 1).Append(" t").Append(DebugText.Muted).Append("  value ")
                    .Append(DebugText.End).Integer(context.PlayerHold.TotalValue).Append(" cr");

            IReadOnlyList<ExtractionPoint> points = ExtractionPoint.All;
            if (points.Count > 0)
            {
                ExtractionPoint nearest = null;
                float nearestDistance = float.MaxValue;
                int open = 0;
                foreach (ExtractionPoint point in points)
                {
                    if (point == null)
                        continue;

                    if (point.State == ExtractionPointState.Open)
                        open++;

                    float distance = point.DistanceTo(context.Focus);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = point;
                    }
                }

                text.Row("ext. points").Integer(points.Count).Append(DebugText.Muted).Append("  open ").Append(DebugText.End).Integer(open);
                if (nearest != null)
                    text.Row("nearest").Number(nearestDistance).Append(" m  ").Append(StateName(nearest.State));
            }

            AuthorityResponse authority = AuthorityResponse.Active;
            if (authority != null)
                text.Row("authority").Integer(authority.ActiveShipCount).Append(DebugText.Muted).Append(" / ").Append(DebugText.End)
                    .Integer(authority.CurrentResponse.MaxShips).Append(" ships");
        }

        private static void WriteAi(StringBuilder text, DebugContext context)
        {
            text.Heading("AI");
            AuthorityResponse authority = AuthorityResponse.Active;
            if (authority == null)
            {
                text.Append('\n').Append(DebugText.Muted).Append("no Authority in this scene").Append(DebugText.End);
                return;
            }

            text.Row("authority").Integer(authority.ActiveShipCount).Append(DebugText.Muted).Append(" / ").Append(DebugText.End)
                .Integer(authority.CurrentResponse.MaxShips).Append(" ships").Append(DebugText.Muted).Append(authority.SpawningEnabled ? "  spawning" : "  spawning off")
                .Append(DebugText.End);

            int rows = 0;
            foreach (Vehicle ship in authority.ActiveShips)
            {
                if (ship == null || ship.Destroyed)
                    continue;

                if (rows++ == MaxShipRows)
                {
                    text.Append('\n').Append(DebugText.Muted).Append("...").Append(DebugText.End);
                    break;
                }

                Rigidbody body = ship.CachedRigidbody;
                text.Append('\n').Append(DebugText.Info).Append(ship.Label).Append(DebugText.End).Append("  ")
                    .Number(Vector3.Distance(context.Focus, ship.transform.position)).Append(" m  ")
                    .Number(body != null ? body.linearVelocity.magnitude : 0f).Append(" m/s");
            }
        }

        private static void WriteSector(StringBuilder text, DebugContext context)
        {
            text.Heading("SECTOR");
            SectorSpace space = SectorSpace.Active;
            if (space == null || space.Definition == null)
            {
                text.Append('\n').Append(DebugText.Muted).Append("not in a generated sector").Append(DebugText.End);
                return;
            }

            SectorDefinition definition = space.Definition;
            text.Append(DebugText.Muted).Append("  ").Append(definition.DisplayName).Append(DebugText.End);
            text.Row("seed").Integer(space.Seed, false);
            if (space.HasSeedOverride)
                text.Append(DebugText.Warn).Append("  override").Append(DebugText.End);
            else
                text.Append(DebugText.Muted).Append(definition.NewSeedEachLoad ? "  new this load" : "  pinned").Append(DebugText.End);
            CellCoord cell = space.FocusCell;
            text.Row("cell").Integer(cell.X, false).Append(", ").Integer(cell.Y, false).Append(", ").Integer(cell.Z, false)
                .Append(DebugText.Muted).Append("  size ").Append(DebugText.End).Number(definition.CellSize).Append(" m");

            Vector3 sectorPosition = space.WorldToSector(context.Focus);
            float density = space.DensityAt(sectorPosition, out int region);
            text.Row("region");
            if (region >= 0 && region < space.Layout.Count)
                text.Append(space.Layout.NameOf(region));
            else
                text.Append(DebugText.Muted).Append("open space").Append(DebugText.End);

            text.Append(DebugText.Muted).Append("  density ").Append(DebugText.End).Number(density, 2);

            AsteroidField field = AsteroidField.Active;
            if (field == null)
                return;

            text.Row("field").Integer(field.DrawnRocks).Append(" drawn").Append(DebugText.Muted).Append(" of ").Append(DebugText.End)
                .Integer(field.LoadedRocks).Append(DebugText.Muted).Append("  in ").Append(DebugText.End).Integer(field.LoadedCells)
                .Append(" cells");
            text.Row("field cost").Number(field.MainThreadMilliseconds, 2).Append(" ms").Append(DebugText.Muted).Append("  ")
                .Append(DebugText.End).Integer(field.DrawCalls).Append(" draw calls");
            text.Row("solid rocks").Integer(field.SolidRockCount);
        }

        private static void WriteDestruction(StringBuilder text)
        {
            text.Heading("DESTRUCTION");
            DestructionSystem system = DestructionSystem.Active;
            if (system == null)
            {
                text.Append('\n').Append(DebugText.Muted).Append("no Destruction System in this scene").Append(DebugText.End);
                return;
            }

            int rocks = 0;
            int pieces = 0;
            long triangles = 0;
            long voxelBytes = 0;
            foreach (VoxelBody body in system.Bodies)
            {
                if (body == null || !body.IsAllocated || body.IsWarmUp)
                    continue;

                if (body.IsPiece)
                    pieces++;
                else
                    rocks++;

                triangles += body.TriangleCount;
                voxelBytes += body.MemoryBytes;
            }

            text.Row("bodies").Integer(rocks).Append(" rocks").Append(DebugText.Muted).Append("  ").Append(DebugText.End).Integer(pieces).Append(" pieces");
            text.Row("time").Number(system.LastFrameMilliseconds, 2).Append(" ms").Append(DebugText.Muted).Append("  peak ").Append(DebugText.End)
                .Number(system.PeakFrameMilliseconds, 2);
            text.Row("meshing").Integer(system.MeshedLastFrame).Append(DebugText.Muted).Append(" last frame  ").Append(DebugText.End)
                .Integer(system.QueuedChunks).Append(" queued");
            text.Row("triangles").Integer(triangles);
            text.Row("memory").Bytes(voxelBytes).Append(DebugText.Muted).Append(" voxels  ").Append(DebugText.End).Bytes(system.ScratchBytes)
                .Append(DebugText.Muted).Append(" buffers").Append(DebugText.End);
            text.Row("chips").Integer(system.ChipCount);
        }

        private static void WriteLog(StringBuilder text)
        {
            text.Heading("LOG");
            int errors = DebugLog.Errors;
            int warnings = DebugLog.Warnings;
            text.Row("errors").Append(errors > 0 ? DebugText.Bad : DebugText.Good).Integer(errors).Append(DebugText.End)
                .Append(DebugText.Muted).Append("  warnings ").Append(DebugText.End).Append(warnings > 0 ? DebugText.Warn : DebugText.Good)
                .Integer(warnings).Append(DebugText.End);

            int count = DebugLog.CopyLatest(logEntries);
            for (int i = 0; i < count; ++i)
            {
                DebugLog.Entry entry = logEntries[i];
                text.Append('\n').Append(DebugText.Muted).Number(entry.Time, 1).Append("s ").Append(DebugText.End)
                    .Append(entry.Type == LogType.Warning ? DebugText.Warn : DebugText.Bad).Append(entry.Message).Append(DebugText.End);
            }
        }

        private static void Milliseconds(StringBuilder text, float milliseconds)
        {
            if (milliseconds > 0f)
                text.Number(milliseconds, 2).Append(" ms");
            else
                text.Append(DebugText.Muted).Append(Missing).Append(DebugText.End);
        }

        private static void Counter(StringBuilder text, DebugFrameStats stats, DebugCounter counter)
        {
            if (stats.Has(counter))
                text.Integer(stats.Value(counter));
            else
                text.Append(DebugText.Muted).Append(Missing).Append(DebugText.End);
        }

        private static void BytesCounter(StringBuilder text, DebugFrameStats stats, DebugCounter counter)
        {
            if (stats.Has(counter))
                text.Bytes(stats.Value(counter));
            else
                text.Append(DebugText.Muted).Append(Missing).Append(DebugText.End);
        }

        private static string StateName(ExtractionPointState state)
        {
            switch (state)
            {
                case ExtractionPointState.Uplinking:
                    return "uplinking";
                case ExtractionPointState.Open:
                    return "open";
                default:
                    return "dormant";
            }
        }

        private static string OutcomeName(RunOutcome outcome)
        {
            return outcome == RunOutcome.Extracted ? "extracted" : "lost";
        }
    }
}
#endif
