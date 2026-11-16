using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// One body's batch of cuts and the buffers its carve job writes. The scheduler keeps two: one run inline for
    /// everyday craters, and one run on a worker for heavy cuts that cross a whole rock.
    /// </summary>
    internal sealed class CarveBatch : IDisposable
    {
        private const int MinCapacity = 32;

        public readonly List<CarveCommand> Commands = new List<CarveCommand>();

        public NativeArray<CarveRequest> Requests;

        /// <summary><see cref="VoxelEncoding.MaterialSlots"/> per command, in voxel volumes.</summary>
        public NativeArray<float> Removed;

        /// <summary>One per command: the rock removed, as mass moments in grid coordinates.</summary>
        public NativeArray<MassMoments> Moments;

        /// <summary>Two per command: lowest and highest changed sample. Min above max when nothing changed.</summary>
        public NativeArray<int3> Changed;

        /// <summary>The body a worker batch is cutting, and its version when the job started.</summary>
        public VoxelBody Body;
        public int Version;
        public JobHandle Handle;
        public bool InFlight;

        /// <summary>Moves the first <paramref name="count"/> of the body's waiting cuts into this batch.</summary>
        public void Take(VoxelBody body, int count)
        {
            Commands.Clear();
            for (int i = 0; i < count; ++i)
                Commands.Add(body.PendingCarves[i]);

            body.PendingCarves.RemoveRange(0, count);
        }

        /// <summary>The job that applies this batch to the body's grid.</summary>
        public VoxelCarveJob CreateJob(VoxelBody body)
        {
            int count = Commands.Count;
            EnsureCapacity(count);
            for (int i = 0; i < count; ++i)
                Requests[i] = Commands[i].Request;

            return new VoxelCarveJob
            {
                Density = body.Density,
                Material = body.Material,
                Size = body.GridSize,
                Requests = Requests,
                Count = count,
                Removed = Removed,
                Moments = Moments,
                Changed = Changed
            };
        }

        public void Dispose()
        {
            Handle.Complete();
            InFlight = false;
            Body = null;
            DisposeArrays();
        }

        private void DisposeArrays()
        {
            if (Requests.IsCreated)
                Requests.Dispose();
            if (Removed.IsCreated)
                Removed.Dispose();
            if (Moments.IsCreated)
                Moments.Dispose();
            if (Changed.IsCreated)
                Changed.Dispose();
        }

        private void EnsureCapacity(int count)
        {
            if (Requests.IsCreated && Requests.Length >= count)
                return;

            DisposeArrays();
            int capacity = math.max(count, MinCapacity);
            Requests = new NativeArray<CarveRequest>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Removed = new NativeArray<float>(capacity * VoxelEncoding.MaterialSlots, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Moments = new NativeArray<MassMoments>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Changed = new NativeArray<int3>(capacity * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
    }
}
