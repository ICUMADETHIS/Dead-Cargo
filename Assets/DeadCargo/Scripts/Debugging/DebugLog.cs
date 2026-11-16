#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System.Diagnostics;
using UnityEngine;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// Counts warnings and errors as they are logged, from any thread, and keeps the latest few for the Log channel.
    /// Plain log messages are ignored, so it costs nothing while the game runs cleanly.
    /// </summary>
    public static class DebugLog
    {
        /// <summary>Latest messages kept.</summary>
        public const int Capacity = 6;

        private const int MaxMessageLength = 160;

        private static readonly object gate = new object();
        private static readonly Entry[] entries = new Entry[Capacity];
        private static readonly Stopwatch clock = new Stopwatch();
        private static int next;
        private static int stored;
        private static int warnings;
        private static int errors;
        private static bool listening;

        /// <summary>One logged warning or error.</summary>
        public struct Entry
        {
            public LogType Type;

            /// <summary>Seconds since logging started.</summary>
            public float Time;

            /// <summary>The message's first line, shortened.</summary>
            public string Message;
        }

        public static int Warnings
        {
            get
            {
                lock (gate)
                    return warnings;
            }
        }

        /// <summary>Errors, exceptions and failed assertions.</summary>
        public static int Errors
        {
            get
            {
                lock (gate)
                    return errors;
            }
        }

        public static void Clear()
        {
            lock (gate)
            {
                next = stored = warnings = errors = 0;
                System.Array.Clear(entries, 0, entries.Length);
            }
        }

        /// <summary>Copies the latest messages, newest first, and returns how many were copied.</summary>
        /// <param name="into">At least <see cref="Capacity"/> long.</param>
        public static int CopyLatest(Entry[] into)
        {
            lock (gate)
            {
                for (int i = 0; i < stored; ++i)
                    into[i] = entries[(next - 1 - i + Capacity) % Capacity];

                return stored;
            }
        }

        internal static void Start()
        {
            if (listening)
                return;

            listening = true;
            clock.Restart();
            Clear();
            Application.logMessageReceivedThreaded += OnLogged;
        }

        internal static void Stop()
        {
            if (!listening)
                return;

            listening = false;
            Application.logMessageReceivedThreaded -= OnLogged;
        }

        private static void OnLogged(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
                return;

            string message = condition ?? string.Empty;
            int lineEnd = message.IndexOf('\n');
            int length = Mathf.Min(lineEnd >= 0 ? lineEnd : message.Length, MaxMessageLength);
            if (length < message.Length)
                message = message.Substring(0, length);

            lock (gate)
            {
                if (type == LogType.Warning)
                    warnings++;
                else
                    errors++;

                entries[next] = new Entry { Type = type, Time = (float)clock.Elapsed.TotalSeconds, Message = message };
                next = (next + 1) % Capacity;
                stored = Mathf.Min(stored + 1, Capacity);
            }
        }
    }
}
#endif
