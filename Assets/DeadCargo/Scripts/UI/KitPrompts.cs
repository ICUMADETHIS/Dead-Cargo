using System;
using System.Collections.Generic;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>
    /// The kit's own prompts on the player's ship, such as PRESS L TO LAUNCH, read so the run HUD can show them as a comic
    /// tag. The kit keeps writing its prompt texts; while bound they are only made invisible, and they show again on
    /// unbind. The kit moves its HUD canvases onto the camera and gives no public handle on the texts, so they are read
    /// from the prompt components' own serialized field.
    /// </summary>
    internal sealed class KitPrompts
    {
        private const string PromptField = "promptText";
        private const string Press = "PRESS ";
        private const string To = " TO ";

        // The kit components that write a prompt: launching and landing, and boarding and leaving (the ship's own
        // ShipEnterExitManager derives from VehicleEnterExitManager, which declares the field).
        private static readonly Type[] PromptWriters =
            { typeof(HUDShipLander), typeof(VehicleEnterExitManager), typeof(HUDVehicleEnterExit) };

        private readonly List<UVCText> texts = new List<UVCText>();
        private readonly List<CanvasGroup> hiders = new List<CanvasGroup>();
        private readonly List<bool> addedHiders = new List<bool>();
        private readonly List<float> hiddenAlphas = new List<float>();
        private string parsed;

        /// <summary>The key the prompt names, upper case, or null when it names none.</summary>
        internal string Key { get; private set; }

        /// <summary>What the prompt asks for, upper case, such as LAUNCH.</summary>
        internal string Action { get; private set; }

        /// <summary>Finds the ship's prompt texts and hides them, after releasing any from an earlier ship.</summary>
        internal void Bind(Vehicle vehicle)
        {
            Unbind();
            if (vehicle == null)
                return;

            foreach (Type writer in PromptWriters)
            {
                System.Reflection.FieldInfo field = writer.GetField(PromptField,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null)
                    continue;

                foreach (Component component in vehicle.GetComponentsInChildren(writer, true))
                {
                    if (field.GetValue(component) is UVCText text && text != null && !texts.Contains(text))
                        Hide(text);
                }
            }
        }

        /// <summary>Shows the kit's prompt texts again.</summary>
        internal void Unbind()
        {
            for (int i = 0; i < hiders.Count; ++i)
            {
                CanvasGroup hider = hiders[i];
                if (hider == null)
                    continue;

                if (addedHiders[i])
                    UnityEngine.Object.Destroy(hider);
                else
                    hider.alpha = hiddenAlphas[i];
            }

            texts.Clear();
            hiders.Clear();
            addedHiders.Clear();
            hiddenAlphas.Clear();
        }

        /// <summary>
        /// True while one of the ship's prompts has text; <see cref="Key"/> and <see cref="Action"/> then hold it. The text
        /// is only split again when it changes, so a prompt the kit rewrites every frame costs nothing.
        /// </summary>
        internal bool Read()
        {
            foreach (UVCText text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy)
                    continue;

                string raw = text.text;
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                if (!string.Equals(raw, parsed, StringComparison.Ordinal))
                    Parse(raw);

                return true;
            }

            return false;
        }

        private void Hide(UVCText text)
        {
            bool added = !text.TryGetComponent(out CanvasGroup hider);
            if (added)
                hider = text.gameObject.AddComponent<CanvasGroup>();

            texts.Add(text);
            hiders.Add(hider);
            addedHiders.Add(added);
            hiddenAlphas.Add(hider.alpha);
            hider.alpha = 0f;
        }

        // The kit's prompts read PRESS <key> TO <action>; anything else is shown whole, with no key.
        private void Parse(string raw)
        {
            parsed = raw;
            string prompt = raw.Trim();
            int to = prompt.StartsWith(Press, StringComparison.OrdinalIgnoreCase)
                ? prompt.IndexOf(To, Press.Length, StringComparison.OrdinalIgnoreCase)
                : -1;

            if (to > Press.Length)
            {
                Key = prompt.Substring(Press.Length, to - Press.Length).Trim().ToUpperInvariant();
                Action = prompt.Substring(to + To.Length).Trim().ToUpperInvariant();
            }
            else
            {
                Key = null;
                Action = prompt.ToUpperInvariant();
            }
        }
    }
}
