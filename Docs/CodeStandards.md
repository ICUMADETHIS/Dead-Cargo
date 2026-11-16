# Dead Cargo: Code Standards

## Layout
```
Assets/DeadCargo/
  Scripts/<Feature>/   runtime code, one feature per folder
  Scripts/UI/          HUD and menus
  Editor/              editor-only tooling
  Data/<Feature>/      ScriptableObject assets
  Resources/           assets found at runtime by name (HUD fonts, scene flow settings, cargo catalog)
  Scenes/              game scenes (Mothership)
  DevTest/             test range, its template and the dev console
Docs/                  design, roadmap, standards
```

## Naming
| Element | Convention | Example |
|---|---|---|
| Namespace | `DeadCargo.<Feature>` | `DeadCargo.Cargo` |
| Type, method, property, event | PascalCase | `CargoHold`, `TotalMass` |
| Private and serialized field | camelCase | `massCapacity` |
| Constant | PascalCase | `DataFolder` |
| Data asset type | `<Thing>Definition` | `CargoItemDefinition` |
| Event | Past tense or state noun | `Changed`, `CapacityExceeded` |
| Bool | `Is`, `Has`, `Can` prefix | `IsEmpty` |

One public type per file. The file name matches the type.

## Classes
- `sealed` unless designed for inheritance.
- Serialized fields are `[SerializeField] private` with a `[Tooltip]` and a range or `[Min]` where a limit exists.
- State is exposed through read-only properties. Nothing public is settable without a method.
- Systems talk through C# events, not by polling each other in `Update`.
- No per-frame allocations in gameplay code.

## Comments
- XML `<summary>` on public types and non-obvious public members.
- Inline comments explain why, not what. No commented-out code.
- Units go in tooltips and summaries (tonnes, credits, seconds).

## Kit integration
- Kit files under `Assets/SpaceCombatKit/` and `Assets/FlatKit/` are never edited. Extend through components, events and public API.
- The kit compiles into the default assembly, so Dead Cargo code does too. Assembly definitions are deferred until the kit is isolated.

## Scenes
- Work happens in the scene that is open. Setup tools act on the active scene, support Undo, and are safe to run twice.
- **The Dev Test Range stays blank.** Test content is spawned into it on demand from the God Menu **Range** tab, and removed when the test is done. Content comes from the template scene `Assets/DeadCargo/DevTest/Templates/TestRangeTemplate.unity`, a saved copy of the fully built range.
- To make new content spawnable on its own, add it to the template and add a `RangeGroup` line in `Editor/TestRange/TestRangeLibrary.cs`. Do not leave new test objects permanently in the range.
- After a phase adds objects to the player rig, spawn everything, run the phase setup, and save the scene as the template again so the template stays current.
- New scenes are created only when the design calls for one, through a setup tool. The game has a **Mothership** scene and **sector** scenes, and moves between them through `SceneFlow`, never by loading scenes directly.

## Hierarchy
Every scene follows one layout, so it stays readable as the game grows.

| Root | Holds |
|---|---|
| `Systems` | Dead Cargo scene services and managers: Heat Tracker, Authority Response, Run Tracker, EventSystem, dev bootstrap |
| `UI` | Canvases and screens: Extraction HUD, Run Result Screen, Mothership Hub |
| `Cameras` | Cameras that are not part of the kit's camera rig |
| `Environment` | Lights, probes and set dressing |
| Kit objects | `SceneObjects`, `VehicleCamera`, `Player`, ships and characters stay **at the root** |
| Range content | `Level`, `Cargo Pickups` stay at the root, because the Range tab spawns them by root name |

- **Ships, characters and game agents must stay at the root.** The kit finds a ship's parts through `transform.root` for hit detection, radar and boarding. A ship inside a folder breaks all three. Ships spawned at runtime go at the root for the same reason.
- Folders are empty objects at the origin. They sit at the top of the hierarchy in the order above.
- Setup tools create objects through `SceneHierarchy.EnsureObject`, which files them into the right folder. Do not create loose root objects.
- **Dead Cargo > Tidy Hierarchy (Open Scene)**, also in the God Menu **Project** tab, files existing objects into folders. It supports Undo. Superseded objects are deleted, not hidden.

## Save data
- All saved progress lives in one `PlayerProfile`, changed only through `ProfileStore`, which saves after every change.
- Save data refers to assets by stable ID (`CargoItemDefinition.Id`), never by name or path, and resolves IDs through a catalog.
- Never change an item's ID once saves exist. Change the format by raising `PlayerProfile.CurrentVersion` and migrating in `ProfileStore`.

## Units
| Quantity | Unit |
|---|---|
| Cargo mass | tonnes (t) |
| Value | credits (cr) |
| Distance | metres |
| Time | seconds |

## God Menu
The God Menu (`Dead Cargo > God Menu`, Ctrl+Alt+G) is the editor control panel for testing. Code lives in `Assets/DeadCargo/Editor/GodMenu/`.

- **One tab per system.** Each tab is a class deriving from `GodMenuTab` in `GodMenu/Tabs/`. The window discovers tabs automatically, so a new tab is one new file.
- **Tabs collapse.** Every tab is a header bar that folds its contents away. Only the first tab starts open. The button at the left of the window header collapses or expands them all.
- **Order** sets the position in the window. Existing values step in tens (Session 0, Range 5, Player 10, Cargo 20, Heat 30, Authority 35, Extraction 38, World 40, Mothership 45, Project 1000) so tabs can be inserted between them.
- **Sections** group controls inside a tab and also collapse. Wrap each group in `GodMenuGUI.SectionScope` and return early when it is not expanded. Keep a tab to a few sections.
- **Shared look.** Use `GodMenuGUI` for buttons, bars and status rows. Do not create styles inside a tab.
- **Fits a narrow window.** The window is often docked in a thin column. Lay out buttons with `GodMenuGUI.ButtonRow`, which wraps onto extra lines, and list rows with `BeginItemRow` / `EndItemRow`, which drop their buttons below the label when space is tight. Do not place several controls in a raw horizontal group, and do not give controls a fixed minimum width.
- **Shared state.** Look up the player, hold and assets through `GodMenuContext`.
- **A phase that adds a system** adds or extends its tab, and adds one line to the phase list in `ProjectTab`.
- Tabs only call public game API. If a cheat needs something new, add a documented method to the system rather than reaching into private state.
