#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System.Text;
using UnityEngine;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// Writes overlay text into a <see cref="StringBuilder"/> without allocating: numbers are written digit by digit
    /// instead of through ToString, and colours are constant rich-text tags. Main thread only.
    /// </summary>
    internal static class DebugText
    {
        public const string Title = "<color=#FFC34D><b>";
        public const string TitleEnd = "</b></color>";
        public const string Muted = "<color=#8E96A3>";
        public const string Good = "<color=#6FD98A>";
        public const string Warn = "<color=#FFD24D>";
        public const string Bad = "<color=#FF6B5E>";
        public const string Info = "<color=#7FC8FF>";
        public const string End = "</color>";

        // Width of the label column, in characters. The font is monospaced, so values line up.
        private const int LabelWidth = 11;

        private static readonly long[] powersOfTen = { 1, 10, 100, 1000, 10000, 100000 };
        // Room for the largest long: 19 digits and 6 separators.
        private static readonly char[] digits = new char[32];

        /// <summary>A panel's heading.</summary>
        public static StringBuilder Heading(this StringBuilder text, string title)
        {
            return text.Append(Title).Append(title).Append(TitleEnd);
        }

        /// <summary>Starts a new line with a muted label, padded so values line up.</summary>
        public static StringBuilder Row(this StringBuilder text, string label)
        {
            text.Append('\n').Append(Muted).Append(label).Append(End);
            for (int i = label.Length; i < LabelWidth; ++i)
                text.Append(' ');

            return text;
        }

        /// <summary>A number with a fixed count of decimals, thousands grouped.</summary>
        public static StringBuilder Number(this StringBuilder text, double value, int decimals = 0)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return text.Append('-');

            decimals = Mathf.Clamp(decimals, 0, powersOfTen.Length - 1);
            if (value < 0.0)
            {
                text.Append('-');
                value = -value;
            }

            long scale = powersOfTen[decimals];
            long scaled = (long)System.Math.Round(System.Math.Min(value, 9e15 / scale) * scale);
            Integer(text, scaled / scale, true);
            if (decimals == 0)
                return text;

            text.Append('.');
            long fraction = scaled % scale;
            for (long place = scale / 10; place > 0; place /= 10)
            {
                text.Append((char)('0' + fraction / place));
                fraction %= place;
            }

            return text;
        }

        /// <summary>A whole number, thousands grouped.</summary>
        public static StringBuilder Integer(this StringBuilder text, long value, bool grouped = true)
        {
            if (value < 0)
            {
                text.Append('-');
                value = -value;
            }

            int length = 0;
            do
            {
                if (grouped && length > 0 && length % 4 == 3)
                    digits[length++] = ',';

                digits[length++] = (char)('0' + value % 10);
                value /= 10;
            }
            while (value > 0);

            for (int i = length - 1; i >= 0; --i)
                text.Append(digits[i]);

            return text;
        }

        /// <summary>A size in bytes, in the largest unit that keeps it readable.</summary>
        public static StringBuilder Bytes(this StringBuilder text, long bytes)
        {
            const double Kilo = 1024.0;
            if (bytes < Kilo)
                return text.Integer(bytes).Append(" B");
            if (bytes < Kilo * Kilo)
                return text.Number(bytes / Kilo, 1).Append(" KB");
            if (bytes < Kilo * Kilo * Kilo)
                return text.Number(bytes / (Kilo * Kilo), 1).Append(" MB");

            return text.Number(bytes / (Kilo * Kilo * Kilo), 2).Append(" GB");
        }

        /// <summary>A world position as x, y, z in whole metres.</summary>
        public static StringBuilder Position(this StringBuilder text, Vector3 position)
        {
            return text.Number(position.x).Append(", ").Number(position.y).Append(", ").Number(position.z);
        }

        /// <summary>A duration as minutes and seconds.</summary>
        public static StringBuilder Clock(this StringBuilder text, float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            text.Integer(whole / 60, false).Append(':');
            int rest = whole % 60;
            if (rest < 10)
                text.Append('0');

            return text.Integer(rest, false);
        }

        /// <summary>Opens a colour for a frame time against the 60 and 30 fps budgets.</summary>
        public static StringBuilder FrameColour(this StringBuilder text, float milliseconds)
        {
            return text.Append(milliseconds <= 1000f / 58f ? Good : milliseconds <= 1000f / 29f ? Warn : Bad);
        }

        /// <summary>Opens a colour for a share where higher is worse: under half good, under 85% a warning.</summary>
        public static StringBuilder LoadColour(this StringBuilder text, float share)
        {
            return text.Append(share < 0.5f ? Good : share < 0.85f ? Warn : Bad);
        }
    }
}
#endif
