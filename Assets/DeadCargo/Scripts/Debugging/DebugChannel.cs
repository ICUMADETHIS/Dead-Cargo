#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using UnityEngine;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// One kind of debug information the overlay can show. Each channel has a panel of live readouts, and most also
    /// draw in the world. The order here is the order of the panels and of the channel picker's keys.
    /// </summary>
    public enum DebugChannel
    {
        Performance,
        Rendering,
        Memory,
        Physics,
        Camera,
        Player,
        Gameplay,
        Ai,
        Sector,
        Destruction,
        Log
    }

    /// <summary>Names, descriptions and picker keys of the <see cref="DebugChannel"/>s.</summary>
    public static class DebugChannels
    {
        public const int Count = 11;

        private static readonly string[] names =
        {
            "Performance", "Rendering", "Memory", "Physics", "Camera", "Player", "Gameplay", "AI", "Sector", "Destruction", "Log"
        };

        private static readonly string[] descriptions =
        {
            "Frame rate, frame time graph, 1% low, main thread and GPU time, overlay cost.",
            "Batches, SetPass calls, draw calls, triangles, vertices, shadow casters, resolution and quality.",
            "Total, managed and graphics memory, garbage per frame and collections.",
            "Physics bodies and contacts. In the world: rigidbodies near the camera, coloured by state, with velocity.",
            "Camera position, facing, field of view, clip planes and speed.",
            "The player's ship: speed, health, target, cargo. In the world: velocity, facing and target line.",
            "The run: heat, cargo, jump drive, extraction points, scene. In the world: extraction points and cargo pickups.",
            "Authority ships: count, tier limits, each pilot's state. In the world: each ship's label, and a line to the player while it fights or to where it is flying.",
            "The sector: seed, cell and region at the camera. In the world: sector bounds, regions and cells.",
            "Voxel rock: bodies, cost, memory. In the world: each rock's centre of mass, drift, spin and mass.",
            "Warnings and errors, with the latest messages."
        };

        private static readonly KeyCode[] keys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus
        };

        private static readonly string[] keyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-" };

        public static string Name(DebugChannel channel)
        {
            return names[(int)channel];
        }

        public static string Description(DebugChannel channel)
        {
            return descriptions[(int)channel];
        }

        /// <summary>The key that switches the channel while the channel picker is open.</summary>
        public static KeyCode PickerKey(DebugChannel channel)
        {
            return keys[(int)channel];
        }

        public static string PickerKeyLabel(DebugChannel channel)
        {
            return keyLabels[(int)channel];
        }
    }
}
#endif
