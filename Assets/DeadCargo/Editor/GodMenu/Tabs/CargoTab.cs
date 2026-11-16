using DeadCargo.Cargo;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect and edit the player ship's cargo hold.</summary>
    internal sealed class CargoTab : GodMenuTab
    {
        private static readonly string[] HoldLabels = { "Empty Hold", "Jettison All" };

        public override string Title => "Cargo";
        public override int Order => 20;

        public override void OnGUI()
        {
            CargoHold hold = GodMenuContext.Hold;
            if (GodMenuContext.IsPlaying && hold == null)
                EditorGUILayout.HelpBox("No cargo hold in the scene. Run the Phase 1 setup from the Project tab.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(hold == null))
            {
                DrawHold(hold);
                DrawContents(hold);
                DrawAddCargo(hold);
            }
        }

        private static void DrawHold(CargoHold hold)
        {
            using (var section = new GodMenuGUI.SectionScope("Hold"))
            {
                if (!section.Expanded)
                    return;

                float mass = hold != null ? hold.TotalMass : 0f;
                float capacity = hold != null ? hold.MassCapacity : 0f;
                float load = hold != null ? hold.LoadFraction : 0f;

                GodMenuGUI.Bar(load, $"{mass:0.#} / {capacity:0.#} t", GodMenuGUI.Accent);
                GodMenuGUI.Field("Value", hold != null ? $"{hold.TotalValue:N0} cr" : "0 cr");

                CargoJettison jettison = hold != null ? hold.GetComponent<CargoJettison>() : null;
                bool[] enabled = { true, jettison != null };

                int clicked = GodMenuGUI.ButtonRow(HoldLabels, enabled);
                if (hold == null)
                    return;

                if (clicked == 0)
                    hold.Clear();
                else if (clicked == 1)
                    jettison.JettisonAll();
            }
        }

        private static void DrawContents(CargoHold hold)
        {
            using (var section = new GodMenuGUI.SectionScope("Contents"))
            {
                if (!section.Expanded)
                    return;

                if (hold == null || hold.IsEmpty)
                {
                    GUILayout.Label("Empty", EditorStyles.miniLabel);
                    return;
                }

                CargoJettison jettison = hold.GetComponent<CargoJettison>();

                // Iterates backwards and stops after an edit, because buttons change the list.
                for (int i = hold.Stacks.Count - 1; i >= 0; --i)
                {
                    CargoStack stack = hold.Stacks[i];

                    GodMenuGUI.BeginItemRow($"{stack.Item.DisplayName}  x{stack.Quantity}", $"{stack.Mass:0.#} t");
                    bool removeOne = GodMenuGUI.MiniButton("-1", 30f);
                    bool removeAll = GodMenuGUI.MiniButton("Remove", 60f);
                    bool eject = jettison != null && GodMenuGUI.MiniButton("Jettison", 60f);
                    GodMenuGUI.EndItemRow();

                    if (removeOne)
                        hold.Remove(stack.Item, 1);
                    else if (removeAll)
                        hold.Remove(stack.Item, stack.Quantity);
                    else if (eject)
                        jettison.Jettison(stack.Item, stack.Quantity);

                    if (removeOne || removeAll || eject)
                        break;
                }
            }
        }

        private static void DrawAddCargo(CargoHold hold)
        {
            using (var section = new GodMenuGUI.SectionScope("Add Cargo"))
            {
                if (!section.Expanded)
                    return;

                if (GodMenuContext.CargoItems.Count == 0)
                {
                    GUILayout.Label("No cargo item assets in the project.", EditorStyles.miniLabel);
                    return;
                }

                foreach (CargoItemDefinition item in GodMenuContext.CargoItems)
                {
                    GodMenuGUI.BeginItemRow(item.DisplayName, $"{item.UnitMass:0.#} t  ·  {item.UnitValue:N0} cr");
                    bool addOne = GodMenuGUI.MiniButton("+1", 30f);
                    bool addFive = GodMenuGUI.MiniButton("+5", 30f);
                    bool fill = GodMenuGUI.MiniButton("Fill", 40f);
                    GodMenuGUI.EndItemRow();

                    if (hold == null)
                        continue;

                    if (addOne)
                        hold.Add(item, 1);
                    else if (addFive)
                        hold.Add(item, 5);
                    else if (fill)
                        hold.Add(item, hold.GetFreeUnits(item));
                }
            }
        }
    }
}
