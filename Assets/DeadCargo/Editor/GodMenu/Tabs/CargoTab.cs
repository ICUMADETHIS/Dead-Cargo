using DeadCargo.Cargo;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect and edit the player ship's cargo hold, with a small picture of its grid.</summary>
    internal sealed class CargoTab : GodMenuTab
    {
        private static readonly string[] HoldLabels = { "Empty Hold", "Jettison All" };

        // The grid picture, in editor pixels.
        private const float PreviewCell = 14f;
        private const float PreviewGap = 1f;
        private static readonly Color PreviewFree = new Color(0.16f, 0.17f, 0.2f);
        private static readonly Color PreviewSelected = new Color(0.95f, 0.93f, 0.89f);

        public override string Title => "Cargo";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 10;

        public override bool IsAvailable => GodMenuContext.Hold != null;

        public override string Summary
        {
            get
            {
                CargoHold hold = GodMenuContext.Hold;
                if (hold == null)
                    return null;

                string darkMatter = hold.DarkMatterUnits > 0 ? $"  ·  {hold.DarkMatterUnits} DM" : "";
                return $"{hold.TotalMass:0.#} / {hold.MassCapacity:0.#} t  ·  {hold.UsedCells}/{hold.CellCount} cells{darkMatter}";
            }
        }

        public override void OnGUI()
        {
            CargoHold hold = GodMenuContext.Hold;
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
                GodMenuGUI.Field("Space", hold != null ? $"{hold.UsedCells} / {hold.CellCount} cells" : "0");
                GodMenuGUI.Field("Dark Matter", hold != null ? $"{hold.DarkMatterUnits} / {hold.DarkMatterCapacity}" : "0");
                GodMenuGUI.Field("Value", hold != null ? $"{hold.TotalValue:N0} cr" : "0 cr");

                CargoJettison jettison = hold != null ? hold.GetComponent<CargoJettison>() : null;
                if (hold != null)
                    DrawGrid(hold, jettison != null ? jettison.SelectedId : 0);
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

        // Each unit in its item's colour, the selected one outlined, as the hold panel shows it.
        private static void DrawGrid(CargoHold hold, int selectedId)
        {
            float step = PreviewCell + PreviewGap;
            Rect area = GUILayoutUtility.GetRect(hold.GridColumns * step, hold.GridRows * step, GUILayout.ExpandWidth(false));
            if (Event.current.type != EventType.Repaint)
                return;

            for (int y = 0; y < hold.GridRows; ++y)
            {
                for (int x = 0; x < hold.GridColumns; ++x)
                    EditorGUI.DrawRect(new Rect(area.x + x * step, area.y + y * step, PreviewCell, PreviewCell), PreviewFree);
            }

            foreach (CargoPlacement unit in hold.Placements)
            {
                Vector2Int size = unit.Size;
                var block = new Rect(area.x + unit.Cell.x * step, area.y + unit.Cell.y * step, size.x * step - PreviewGap,
                    size.y * step - PreviewGap);
                if (unit.Id == selectedId)
                    EditorGUI.DrawRect(new Rect(block.x - 1f, block.y - 1f, block.width + 2f, block.height + 2f), PreviewSelected);

                EditorGUI.DrawRect(block, unit.Item.BlockColor);
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
                    string size = item.Slot == CargoSlot.Hold ? $"{item.Size.x}×{item.Size.y}  ·  " : "";
                    GodMenuGUI.BeginItemRow(item.DisplayName, $"{size}{item.UnitMass:0.#} t  ·  {item.UnitValue:N0} cr");
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
