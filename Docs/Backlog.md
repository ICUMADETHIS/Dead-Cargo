# Dead Cargo: Polish Backlog

Issues found in testing that were deferred. Review this list before each milestone closes.

## Open

None.

## Later

Features the user wants, parked until their time comes.

| Idea | Requested | Notes |
|---|---|---|
| Authority blockade at HUNTED | 2026-11-16 | While the player spools at HUNTED, the Authority forms a blockade nearby. Left out of phase 6.1 (riskier extraction) on purpose. |
| Comic look on every ship | 2026-11-16 | Phase 6.9 step 4 converts the player's ship in the open scene. Authority ships spawned at runtime, and other kit ships, still use Flat Kit. Convert them as their own Dead Cargo prefabs or swap materials on spawn. |
| Remove the old star sky | 2026-11-16 | `Materials/DeepSpaceSkybox.mat` (phase 6.7a) is unused once the space sky passes its test. Delete it then. |

## Resolved

| # | Found | Phase | Issue | Resolution |
|---|---|---|---|---|
| 1 | 2026-11-16 | 2 | Jettisoned crates do not visibly drift apart | Fixed 2026-11-16. The sideways scatter used `Random.insideUnitSphere`, which often gave almost no speed, and Jettison All spawned every crate at the same point. Each crate now gets the full scatter speed at right angles to its exit, and Jettison All fans crates out around the ejection point (`Dump Spacing`, 4 m). |
| 2 | 2026-11-16 | 1, 3 | Delete the superseded `CargoHudPanel` and `HeatHudPanel` scripts | Deleted 2026-11-16, after no scene, prefab or code used them. |
