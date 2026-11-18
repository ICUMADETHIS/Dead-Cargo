using System.Collections.Generic;
using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The hold view on I (phase 6.15, concept B): an inked panel on the left holding the hold's mass and handling, the
    /// sealed dark matter tank, the hold grid and the selected unit. The mouse picks a unit, drags it to a new place (R turns
    /// it while it is held), or drags it out of the panel to dump it. J, read by the cargo controls, dumps the selected unit.
    /// Text is rebuilt only when the hold changes; the dragged block moves every frame. Positions are in reference pixels
    /// from the top-left corner, with rows going down.
    /// </summary>
    internal sealed class CargoHoldPanel
    {
        private const KeyCode TurnKey = KeyCode.R;

        private const float Cell = 60f;
        private const float Pad = 26f;
        private const float MinWidth = 520f;
        private const float ScreenLeft = 40f;
        private const float ScreenTop = 40f;
        private const float BlockInset = 3f;
        private const float SelectionGrow = 5f;
        private const float DraggedAlpha = 0.3f;
        private const float GhostAlpha = 0.92f;

        // How far the mouse moves with the button down before a press becomes a drag, in screen pixels.
        private const float DragThreshold = 8f;

        private const float SlideTime = 0.16f;
        private const float SlideClearance = 60f;

        private const float HeaderHeight = 40f;
        private const float TitleWidth = 140f;
        private const float RiskWidth = 220f;
        private const float MassTop = 80f;
        private const float MassFigureWidth = 200f;
        private const float StatsTop = 124f;
        private const float TankTop = 152f;
        private const float TankHeight = 52f;
        private const float TankBarLeft = 172f;
        private const float TankFigureWidth = 112f;
        private const float GridTop = 222f;
        private const float InfoGap = 16f;
        private const float InfoHeight = 58f;
        private const float InfoTextLeft = 58f;
        private const float InfoKeyWidth = 110f;
        private const float HintGap = 14f;
        private const float HintHeight = 36f;
        private const float KeySize = 26f;

        private const string DumpLabel = "DUMP";
        private const string NothingSelected = "CLICK AN ITEM TO SELECT IT";
        private const string Hints =
            "<color=#EAF2F1>DRAG</color>  MOVE     <color=#EAF2F1>R</color>  TURN WHILE DRAGGING     <color=#EAF2F1>DRAG OUT</color>  DUMP\n" +
            "<color=#EAF2F1>J</color>  DUMP SELECTED     <color=#EAF2F1>HOLD J</color>  DUMP ALL     <color=#EAF2F1>I</color>  CLOSE";

        private static readonly Color PanelFace = new Color32(24, 27, 34, 245);
        private static readonly Color InsetFace = new Color32(15, 17, 22, 255);
        private static readonly Color TankFace = new Color32(21, 16, 38, 255);
        private static readonly Color Track = new Color32(10, 11, 14, 255);
        private static readonly Color InkText = new Color32(11, 12, 16, 255);
        private static readonly Color DropAllowed = WithAlpha(HudFactory.SuccessColor, 0.35f);
        private static readonly Color DropRefused = WithAlpha(HudFactory.WarningColor, 0.35f);

        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly HudSticker body;
        private readonly HudSticker title;
        private readonly Text riskCaption;
        private readonly Text riskValue;
        private readonly Text massFigure;
        private readonly HudSticker massBar;
        private readonly Text stats;
        private readonly HudSticker tank;
        private readonly HudSticker tankBar;
        private readonly Text tankFigure;
        private readonly RectTransform grid;
        private readonly HudGridLines lines;
        private readonly HudSticker selectionFrame;
        private readonly RectTransform blockLayer;
        private readonly HudSticker dropTarget;
        private readonly Block ghost;
        private readonly List<Block> blocks = new List<Block>();
        private readonly HudSticker info;
        private readonly HudSticker infoSwatch;
        private readonly Text infoName;
        private readonly Text infoDetail;
        private readonly HudSticker infoKey;
        private readonly Text infoKeyAction;
        private readonly Text hints;

        private CargoHold hold;
        private CargoJettison jettison;
        private CargoLoadHandling handling;
        private float width;
        private bool open;
        private float shown;
        private int localSelection;

        private int pressedId;
        private Vector2 pressPosition;
        private Vector2 grab;
        private bool dragging;
        private bool dragRotated;
        private bool ghostDumping;
        private bool ghostRotated;
        private Vector2Int targetCell;
        private Vector2Int targetSize;

        internal CargoHoldPanel(RectTransform parent)
        {
            root = HudFactory.Anchored("Cargo Hold", parent, new Vector2(0f, 1f), MinWidth, 600f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            body = Sticker("Body", root, PanelFace, 4f, new Vector2(8f, -8f), 0f);

            title = Sticker("Title", root, StatusStripView.Figure, 3f, new Vector2(4f, -4f), HudSticker.DefaultSlant);
            HudSticker titleKey = Sticker("Key", title.rectTransform, InkText, 0f, Vector2.zero, 0f);
            Put(titleKey, 14f, 6f, KeySize, KeySize);
            Text titleKeyName = Figure("Key Name", titleKey.rectTransform, 18, StatusStripView.Figure, TextAnchor.MiddleCenter);
            Put(titleKeyName, 0f, 0f, KeySize, KeySize);
            Text titleName = Figure("Name", title.rectTransform, 26, InkText, TextAnchor.MiddleLeft);
            titleName.text = "HOLD";
            titleKeyName.text = "I";
            Put(titleName, 50f, -3f, 86f, 44f);

            riskCaption = Caption("At Risk Caption", root, 12, HudFactory.Muted, TextAnchor.MiddleRight);
            riskCaption.text = "AT RISK";
            riskValue = Figure("At Risk", root, 28, StatusStripView.Accent, TextAnchor.MiddleRight);

            massFigure = Figure("Mass", root, 34, StatusStripView.Figure, TextAnchor.MiddleLeft);
            massBar = Sticker("Mass Bar", root, StatusStripView.Accent, 3f, Vector2.zero, HudSticker.DefaultSlant);
            massBar.SetTrack(Track);
            stats = Caption("Handling", root, 12, HudFactory.Muted, TextAnchor.MiddleLeft);

            tank = Sticker("Dark Matter", root, TankFace, 3f, Vector2.zero, 0f);
            Text tankLabel = Caption("Label", tank.rectTransform, 13, HudFactory.DarkMatterColor, TextAnchor.MiddleLeft);
            tankLabel.text = "DARK MATTER";
            Put(tankLabel, 14f, 7f, 150f, 18f);
            Text tankNote = Caption("Note", tank.rectTransform, 10, HudFactory.Muted, TextAnchor.MiddleLeft);
            tankNote.text = "SEALED  ·  WEIGHTLESS";
            Put(tankNote, 14f, 27f, 156f, 16f);
            tankBar = Sticker("Bar", tank.rectTransform, HudFactory.DarkMatterColor, 2f, Vector2.zero, 0f);
            tankBar.SetTrack(Track);
            tankFigure = Figure("Units", tank.rectTransform, 26, HudFactory.DarkMatterColor, TextAnchor.MiddleRight);

            grid = HudFactory.Place("Grid", root, 0f, 0f, Cell, Cell);
            lines = grid.gameObject.AddComponent<HudGridLines>();
            selectionFrame = Sticker("Selection", grid, StatusStripView.Figure, 0f, Vector2.zero, 0f);
            blockLayer = HudFactory.Stretch("Blocks", grid);
            dropTarget = Sticker("Drop Target", grid, DropAllowed, 0f, Vector2.zero, 0f);
            ghost = new Block(grid, "Dragged");
            ghost.SetAlpha(GhostAlpha);
            ghost.SetVisible(false);
            dropTarget.gameObject.SetActive(false);
            selectionFrame.gameObject.SetActive(false);

            info = Sticker("Selected", root, InsetFace, 2f, Vector2.zero, 0f);
            infoSwatch = Sticker("Swatch", info.rectTransform, HudFactory.CargoColor, 2f, Vector2.zero, 0f);
            Put(infoSwatch, 12f, 12f, 34f, 34f);
            infoName = Figure("Name", info.rectTransform, 24, StatusStripView.Figure, TextAnchor.MiddleLeft);
            infoDetail = Caption("Detail", info.rectTransform, 12, HudFactory.Muted, TextAnchor.MiddleLeft);
            infoKey = Sticker("Key", info.rectTransform, InkText, 0f, Vector2.zero, 0f);
            Text infoKeyName = Figure("Key Name", infoKey.rectTransform, 18, StatusStripView.Figure, TextAnchor.MiddleCenter);
            infoKeyName.text = "J";
            Put(infoKeyName, 0f, 0f, KeySize, KeySize);
            infoKeyAction = Caption("Key Action", info.rectTransform, 12, StatusStripView.Figure, TextAnchor.MiddleLeft);
            infoKeyAction.text = DumpLabel;

            hints = Caption("Controls", root, 12, HudFactory.Muted, TextAnchor.UpperLeft);
            hints.supportRichText = true;
            hints.text = Hints;

            Arrange(1, 1);
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => open;

        private int SelectedId => jettison != null ? jettison.SelectedId : localSelection;

        internal void Bind(CargoHold cargoHold, CargoJettison cargoJettison, CargoLoadHandling loadHandling)
        {
            Unbind();
            hold = cargoHold;
            jettison = cargoJettison;
            handling = loadHandling;
            if (jettison != null)
                jettison.SelectionChanged += RefreshSelection;

            Arrange(hold.GridColumns, hold.GridRows);
        }

        internal void Unbind()
        {
            if (jettison != null)
                jettison.SelectionChanged -= RefreshSelection;

            hold = null;
            jettison = null;
            handling = null;
            localSelection = 0;
            open = false;
            shown = 0f;

            // The panel may already be gone while its scene unloads.
            if (root == null)
                return;

            EndDrag();
            root.gameObject.SetActive(false);
        }

        internal void Open()
        {
            if (hold == null)
                return;

            open = true;
            EndDrag();
            Refresh();
        }

        internal void Close()
        {
            open = false;
            EndDrag();
        }

        /// <summary>Slides the panel in or out, and while it is fully in and <paramref name="acceptInput"/>, follows the mouse.</summary>
        internal void Tick(float deltaTime, bool acceptInput)
        {
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, deltaTime / SlideTime);
            bool visible = shown > 0f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
            if (!visible)
                return;

            float eased = 1f - (1f - shown) * (1f - shown);
            root.anchoredPosition = new Vector2(ScreenLeft - (1f - eased) * (width + ScreenLeft + SlideClearance), -ScreenTop);
            group.alpha = eased;

            if (open && acceptInput && hold != null && shown >= 1f)
                HandleMouse();
        }

        /// <summary>Rebuilds the readouts and blocks from the hold. Called when it changes while the panel is open.</summary>
        internal void Refresh()
        {
            if (hold == null)
                return;

            bool full = hold.LoadFraction >= .9999f;
            riskValue.text = $"{hold.TotalValue:N0} CR";
            float riskRight = width - Pad;
            Put(riskCaption, riskRight - riskValue.preferredWidth - 10f - 90f, Pad + 11f, 90f, 18f);
            massFigure.text = $"{hold.TotalMass:0.#} / {hold.MassCapacity:0.#} T";
            massBar.SetFace(full ? HudFactory.WarningColor : StatusStripView.Accent);
            massBar.SetFill(hold.LoadFraction);
            stats.text = HandlingText();

            int capacity = hold.DarkMatterCapacity;
            tankBar.SetFill(capacity > 0 ? (float)hold.DarkMatterUnits / capacity : 0f);
            tankFigure.text = $"{hold.DarkMatterUnits} / {capacity}";

            IReadOnlyList<CargoPlacement> placements = hold.Placements;
            while (blocks.Count < placements.Count)
                blocks.Add(new Block(blockLayer, $"Block {blocks.Count + 1}"));

            for (int i = 0; i < blocks.Count; ++i)
            {
                Block block = blocks[i];
                bool used = i < placements.Count;
                block.SetVisible(used);
                if (!used)
                {
                    block.Id = 0;
                    continue;
                }

                CargoPlacement unit = placements[i];
                block.Id = unit.Id;
                block.SetItem(unit.Item, unit.Size, unit.Item.BlockColor, BlockLabel(unit.Item, unit.Size));
                BlockRect(unit.Cell, unit.Size, out float x, out float y, out float w, out float h);
                block.Place(x, y, w, h);
                block.SetAlpha(dragging && unit.Id == pressedId ? DraggedAlpha : 1f);
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            int id = SelectedId;
            CargoPlacement unit = default;
            bool selected = hold != null && id != 0 && hold.TryGetPlacement(id, out unit);

            selectionFrame.gameObject.SetActive(selected);
            infoSwatch.gameObject.SetActive(selected);
            infoName.gameObject.SetActive(selected);
            infoKey.gameObject.SetActive(selected && jettison != null);
            infoKeyAction.gameObject.SetActive(selected && jettison != null);

            float infoWidth = width - Pad * 2f;
            if (!selected)
            {
                Put(infoDetail, 18f, (InfoHeight - 18f) * 0.5f, infoWidth - 36f, 18f);
                infoDetail.text = NothingSelected;
                return;
            }

            BlockRect(unit.Cell, unit.Size, out float x, out float y, out float w, out float h);
            Put(selectionFrame, x - SelectionGrow, y - SelectionGrow, w + SelectionGrow * 2f, h + SelectionGrow * 2f);

            CargoItemDefinition item = unit.Item;
            Vector2Int size = item.Size;
            int perTonne = item.UnitMass > 0f ? Mathf.RoundToInt(item.UnitValue / item.UnitMass) : 0;
            infoSwatch.SetFace(item.BlockColor);
            infoName.text = item.DisplayName.ToUpperInvariant();
            Put(infoDetail, InfoTextLeft, 33f, infoWidth - InfoTextLeft - InfoKeyWidth, 18f);
            infoDetail.text = $"{size.x}×{size.y}  ·  {item.UnitMass:0.#} T  ·  {item.UnitValue:N0} CR  ·  {perTonne:N0} CR/T";
        }

        private void HandleMouse()
        {
            Vector2 screen = Input.mousePosition;
            bool overPanel = RectTransformUtility.RectangleContainsScreenPoint(root, screen, null);
            Vector2 point = GridPoint(screen);

            if (Input.GetMouseButtonDown(0))
            {
                EndDrag();
                if (overPanel && hold.TryGetPlacementAt(CellAt(point), out CargoPlacement unit))
                {
                    pressedId = unit.Id;
                    pressPosition = screen;
                    grab = point - new Vector2(unit.Cell.x * Cell, unit.Cell.y * Cell);
                    Select(unit.Id);
                }
                else if (overPanel && InGrid(point))
                {
                    Select(0);
                }
            }

            if (pressedId == 0)
                return;

            // The unit can leave mid-drag (J, the God Menu), and a release can be missed while the window is unfocused.
            bool released = Input.GetMouseButtonUp(0);
            if (!hold.TryGetPlacement(pressedId, out CargoPlacement pressed) || (!released && !Input.GetMouseButton(0)))
            {
                EndDrag();
                return;
            }

            if (!dragging)
            {
                if (released)
                    pressedId = 0;
                else if ((screen - pressPosition).sqrMagnitude > DragThreshold * DragThreshold)
                    StartDrag(pressed);

                return;
            }

            if (Input.GetKeyDown(TurnKey))
            {
                dragRotated = !dragRotated;
                grab = new Vector2(grab.y, grab.x);
            }

            Vector2 topLeft = point - grab;
            var cell = new Vector2Int(Mathf.RoundToInt(topLeft.x / Cell), Mathf.RoundToInt(topLeft.y / Cell));
            bool dump = !overPanel;
            bool fits = !dump && hold.CanMove(pressed.Id, cell, dragRotated);
            ShowDrag(pressed, topLeft, cell, dump, fits);

            if (!released)
                return;

            int id = pressed.Id;
            bool rotated = dragRotated;
            EndDrag();
            if (dump)
            {
                if (jettison != null)
                    jettison.JettisonPlacement(id);
            }
            else if (fits)
            {
                hold.Move(id, cell, rotated);
            }
        }

        private void StartDrag(CargoPlacement pressed)
        {
            dragging = true;
            dragRotated = pressed.Rotated;
            ghostDumping = false;
            ghostRotated = !dragRotated;
            targetSize = Vector2Int.zero;
            ghost.SetVisible(true);
            dropTarget.gameObject.SetActive(true);

            Block original = FindBlock(pressed.Id);
            if (original != null)
                original.SetAlpha(DraggedAlpha);
        }

        // Text and sizes change only when the ghost turns or crosses the panel's edge; it moves every frame.
        private void ShowDrag(CargoPlacement pressed, Vector2 topLeft, Vector2Int cell, bool dump, bool fits)
        {
            CargoItemDefinition item = pressed.Item;
            Vector2Int size = CargoPlacement.SizeOf(item.Size, dragRotated);
            BlockRect(Vector2Int.zero, size, out float inset, out _, out float w, out float h);

            if (dump != ghostDumping || dragRotated != ghostRotated)
            {
                ghostDumping = dump;
                ghostRotated = dragRotated;
                ghost.SetItem(item, size, dump ? HudFactory.WarningColor : item.BlockColor, dump ? DumpLabel : BlockLabel(item, size));
                ghost.Place(topLeft.x + inset, topLeft.y + inset, w, h);
            }
            else
            {
                ghost.MoveTo(topLeft.x + inset, topLeft.y + inset);
            }

            if (dropTarget.gameObject.activeSelf != !dump)
                dropTarget.gameObject.SetActive(!dump);
            if (dump)
                return;

            dropTarget.SetFace(fits ? DropAllowed : DropRefused);
            if (cell != targetCell || size != targetSize)
            {
                targetCell = cell;
                targetSize = size;
                Put(dropTarget, cell.x * Cell, cell.y * Cell, size.x * Cell, size.y * Cell);
            }
        }

        private void EndDrag()
        {
            pressedId = 0;
            if (!dragging)
                return;

            dragging = false;
            ghost.SetVisible(false);
            dropTarget.gameObject.SetActive(false);
            for (int i = 0; i < blocks.Count; ++i)
                blocks[i].SetAlpha(1f);
        }

        private void Select(int placementId)
        {
            if (jettison != null)
            {
                jettison.Select(placementId);
                return;
            }

            localSelection = placementId;
            RefreshSelection();
        }

        private Block FindBlock(int placementId)
        {
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].Id == placementId)
                    return blocks[i];
            }

            return null;
        }

        private string HandlingText()
        {
            string cells = $"CELLS  {hold.UsedCells} / {hold.CellCount}";
            if (handling == null || !handling.isActiveAndEnabled)
                return cells;

            int speed = Mathf.RoundToInt(100f * (1f - handling.CurrentSpeedMultiplier));
            int turn = Mathf.RoundToInt(100f * (1f - handling.CurrentTurnMultiplier));
            return $"SPEED  −{speed}%     TURN  −{turn}%     {cells}";
        }

        // The panel is sized by the grid: as wide as its columns need, and tall enough for its rows.
        private void Arrange(int columns, int rows)
        {
            float gridWidth = columns * Cell;
            float gridHeight = rows * Cell;
            width = Mathf.Max(MinWidth, gridWidth + Pad * 2f);
            float inner = width - Pad * 2f;
            float infoTop = GridTop + gridHeight + InfoGap;
            float hintsTop = infoTop + InfoHeight + HintGap;
            float height = hintsTop + HintHeight + Pad;

            root.sizeDelta = new Vector2(width, height);
            Put(body, 0f, 0f, width, height);
            Put(title, Pad, Pad, TitleWidth, HeaderHeight - 2f);
            Put(riskValue, width - Pad - RiskWidth, Pad - 4f, RiskWidth, 46f);
            Put(riskCaption, width - Pad - RiskWidth - 90f, Pad + 11f, 90f, 18f);

            Put(massFigure, Pad, MassTop - 8f, MassFigureWidth, 54f);
            Put(massBar, Pad + MassFigureWidth, MassTop + 10f, inner - MassFigureWidth, 18f);
            Put(stats, Pad, StatsTop, inner, 18f);

            Put(tank, Pad, TankTop, inner, TankHeight);
            Put(tankBar, TankBarLeft, (TankHeight - 18f) * 0.5f, inner - TankBarLeft - TankFigureWidth - 8f, 18f);
            Put(tankFigure, inner - TankFigureWidth - 10f, 2f, TankFigureWidth, 48f);

            Put(grid, (width - gridWidth) * 0.5f, GridTop, gridWidth, gridHeight);
            lines.SetCells(columns, rows);

            Put(info, Pad, infoTop, inner, InfoHeight);
            Put(infoName, InfoTextLeft, 0f, inner - InfoTextLeft - InfoKeyWidth, 38f);
            Put(infoKey, inner - InfoKeyWidth + 14f, (InfoHeight - KeySize) * 0.5f, KeySize, KeySize);
            Put(infoKeyAction, inner - InfoKeyWidth + 48f, (InfoHeight - KeySize) * 0.5f, 56f, KeySize);
            Put(hints, Pad, hintsTop, inner, HintHeight);
        }

        private Vector2 GridPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid, screen, null, out Vector2 local);
            return new Vector2(local.x, -local.y);
        }

        private static Vector2Int CellAt(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / Cell), Mathf.FloorToInt(point.y / Cell));
        }

        private bool InGrid(Vector2 point)
        {
            return point.x >= 0f && point.y >= 0f && point.x < hold.GridColumns * Cell && point.y < hold.GridRows * Cell;
        }

        private static void BlockRect(Vector2Int cell, Vector2Int size, out float x, out float y, out float w, out float h)
        {
            x = cell.x * Cell + BlockInset;
            y = cell.y * Cell + BlockInset;
            w = size.x * Cell - BlockInset * 2f;
            h = size.y * Cell - BlockInset * 2f;
        }

        // Blocks one cell wide carry the short name.
        private static string BlockLabel(CargoItemDefinition item, Vector2Int size)
        {
            return (size.x >= 2 ? item.DisplayName : item.ShortName).ToUpperInvariant();
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static HudSticker Sticker(string name, RectTransform parent, Color face, float outline, Vector2 shadow, float slant)
        {
            HudSticker sticker = HudFactory.Place(name, parent, 0f, 0f, 10f, 10f).gameObject.AddComponent<HudSticker>();
            sticker.Configure(face, outline, shadow, slant);
            return sticker;
        }

        private static Text Caption(string name, RectTransform parent, int size, Color tint, TextAnchor alignment)
        {
            Text text = HudFactory.Label(name, parent, "", 0f, 0f, 10f, 10f, size, tint, alignment);
            text.font = HudFactory.StickerFont;
            HudFactory.Unclipped(text);
            return text;
        }

        private static Text Figure(string name, RectTransform parent, int size, Color tint, TextAnchor alignment)
        {
            Text text = HudFactory.Label(name, parent, "", 0f, 0f, 10f, 10f, size, tint, alignment);
            text.font = HudFactory.FigureFont;
            text.fontStyle = FontStyle.Italic;
            HudFactory.Unclipped(text);
            return text;
        }

        private static void Put(Component element, float x, float y, float w, float h)
        {
            var rect = (RectTransform)element.transform;
            HudFactory.SetOffset(rect, x, y);
            rect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>One unit's block on the grid: an inked sticker in the item's colour, its name at the top, its mass below.</summary>
        private sealed class Block
        {
            private readonly HudSticker body;
            private readonly CanvasGroup group;
            private readonly Text name;
            private readonly Text mass;

            internal Block(RectTransform parent, string objectName)
            {
                body = Sticker(objectName, parent, HudFactory.CargoColor, 3f, new Vector2(3f, -3f), 0f);
                group = body.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                // Best fit needs a box taller than the line, or the label goes blank: Chakra Petch's line is 1.3 times its size.
                name = Caption("Name", body.rectTransform, 11, InkText, TextAnchor.UpperLeft);
                HudFactory.Fit(name, 7);
                mass = Figure("Mass", body.rectTransform, 16, InkText, TextAnchor.LowerRight);
            }

            /// <summary>Placement ID of the unit shown, or 0.</summary>
            internal int Id { get; set; }

            internal void SetItem(CargoItemDefinition item, Vector2Int cells, Color face, string label)
            {
                bool wide = cells.x >= 2;
                body.SetFace(face);
                name.text = label;
                name.alignment = wide ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
                mass.alignment = wide ? TextAnchor.LowerRight : TextAnchor.LowerCenter;
                mass.text = $"{item.UnitMass:0.#} T";
            }

            internal void Place(float x, float y, float w, float h)
            {
                Put(body, x, y, w, h);
                Put(name, 5f, 5f, w - 10f, 16f);
                Put(mass, 4f, h - 31f, w - 10f, 28f);
            }

            internal void MoveTo(float x, float y)
            {
                HudFactory.SetOffset(body.rectTransform, x, y);
            }

            internal void SetAlpha(float alpha)
            {
                if (!Mathf.Approximately(group.alpha, alpha))
                    group.alpha = alpha;
            }

            internal void SetVisible(bool visible)
            {
                if (body.gameObject.activeSelf != visible)
                    body.gameObject.SetActive(visible);
            }
        }
    }
}
