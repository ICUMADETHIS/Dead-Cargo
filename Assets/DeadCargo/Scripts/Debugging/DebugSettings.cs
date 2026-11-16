#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System;
using UnityEngine;

namespace DeadCargo.Debugging
{
    /// <summary>The screen corner the debug overlay sits in.</summary>
    public enum DebugCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// What the debug overlay shows, kept between sessions in PlayerPrefs. Changed from the overlay's keys, the God
    /// Menu and the dev console; all three see the same settings. Readable outside Play Mode.
    /// </summary>
    public static class DebugSettings
    {
        public const float MinTextScale = 0.75f;
        public const float MaxTextScale = 2f;

        private const string Prefix = "DeadCargo.Debug.";
        private const string VisibleKey = Prefix + "Visible";
        private const string ChannelsKey = Prefix + "Channels";
        private const string CornerKey = Prefix + "Corner";
        private const string ScaleKey = Prefix + "TextScale";

        // Only the frame rate counter shows by default: a clean screen with the one number everyone wants.
        private const int DefaultChannels = 0;

        private static bool loaded;
        private static bool overlayVisible;
        private static int channels;
        private static DebugCorner corner;
        private static float textScale;

        /// <summary>Raised whenever any setting changes.</summary>
        public static event Action Changed;

        /// <summary>Whether the overlay shows at all: the frame rate counter and every channel switched on.</summary>
        public static bool OverlayVisible
        {
            get
            {
                Load();
                return overlayVisible;
            }
            set
            {
                Load();
                if (overlayVisible == value)
                    return;

                overlayVisible = value;
                Save();
            }
        }

        public static DebugCorner Corner
        {
            get
            {
                Load();
                return corner;
            }
            set
            {
                Load();
                if (corner == value)
                    return;

                corner = value;
                Save();
            }
        }

        /// <summary>Size of the overlay's text and panels, from <see cref="MinTextScale"/> to <see cref="MaxTextScale"/>.</summary>
        public static float TextScale
        {
            get
            {
                Load();
                return textScale;
            }
            set
            {
                Load();
                value = Mathf.Clamp(value, MinTextScale, MaxTextScale);
                if (Mathf.Approximately(textScale, value))
                    return;

                textScale = value;
                Save();
            }
        }

        /// <summary>Channels switched on.</summary>
        public static int EnabledCount
        {
            get
            {
                Load();
                int count = 0;
                for (int i = 0; i < DebugChannels.Count; ++i)
                {
                    if ((channels & (1 << i)) != 0)
                        count++;
                }

                return count;
            }
        }

        public static bool IsOn(DebugChannel channel)
        {
            Load();
            return (channels & Bit(channel)) != 0;
        }

        public static void Set(DebugChannel channel, bool on)
        {
            Load();
            int next = on ? channels | Bit(channel) : channels & ~Bit(channel);
            if (next == channels)
                return;

            channels = next;
            Save();
        }

        public static void Toggle(DebugChannel channel)
        {
            Set(channel, !IsOn(channel));
        }

        public static void SetAll(bool on)
        {
            Load();
            int next = on ? (1 << DebugChannels.Count) - 1 : 0;
            if (next == channels)
                return;

            channels = next;
            Save();
        }

        public static void ResetToDefaults()
        {
            Load();
            overlayVisible = true;
            channels = DefaultChannels;
            corner = DebugCorner.TopLeft;
            textScale = 1f;
            Save();
        }

        private static int Bit(DebugChannel channel)
        {
            return 1 << (int)channel;
        }

        private static void Load()
        {
            if (loaded)
                return;

            loaded = true;
            overlayVisible = PlayerPrefs.GetInt(VisibleKey, 1) != 0;
            channels = PlayerPrefs.GetInt(ChannelsKey, DefaultChannels);
            corner = (DebugCorner)Mathf.Clamp(PlayerPrefs.GetInt(CornerKey, 0), 0, 3);
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), MinTextScale, MaxTextScale);
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(VisibleKey, overlayVisible ? 1 : 0);
            PlayerPrefs.SetInt(ChannelsKey, channels);
            PlayerPrefs.SetInt(CornerKey, (int)corner);
            PlayerPrefs.SetFloat(ScaleKey, textScale);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
#endif
