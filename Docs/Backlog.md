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
| Rocks collide with each other | 2026-11-16 | Since phase 6.10 step 1b every voxel rock drifts and tumbles, moved kinematically, so rocks pass through each other. Rare while rocks are sparse; worth solving with the asteroid field (6.10 step 3): bounce on overlap using the masses the rocks already track. |
| Floating origin for big sectors | 2026-11-16 | The kit's floating origin is off (`FloatingOriginGuard`), which is fine while sectors fit in ±20 km. A larger sector needs every Dead Cargo system to follow `SectorSpace.OriginOffset` when the world shifts: voxel rock and its rigidbodies, trail rocks, chip particles, cargo, extraction points and generated content. Then remove the guard. |
| Fast ships and small rocks | 2026-11-16 | Kit ships use discrete collision detection. The same applies to solid field rocks (6.8 step 5a): a ship at full boost may end up inside a rock. Trail rocks (6.10 step 1c) are caught by the ship's long hull at normal speeds, but at top speed a ship may pass through a small one. If testing shows it, switch the player ship's rigidbody to speculative collision detection. |
| Remove the old star sky | 2026-11-16 | `Materials/DeepSpaceSkybox.mat` (phase 6.7a) is unused once the space sky passes its test. Delete it then. |

## Resolved

| # | Found | Phase | Issue | Resolution |
|---|---|---|---|---|
| 1 | 2026-11-16 | 2 | Jettisoned crates do not visibly drift apart | Fixed 2026-11-16. The sideways scatter used `Random.insideUnitSphere`, which often gave almost no speed, and Jettison All spawned every crate at the same point. Each crate now gets the full scatter speed at right angles to its exit, and Jettison All fans crates out around the ejection point (`Dump Spacing`, 4 m). |
| 2 | 2026-11-16 | 1, 3 | Delete the superseded `CargoHudPanel` and `HeatHudPanel` scripts | Deleted 2026-11-16, after no scene, prefab or code used them. |
| 3 | 2026-11-16 | 6.10 | The ship seems to jump back towards the start after flying about 1 km from the sector's centre | Fixed 2026-11-16. The kit's Floating Origin Manager (in `SceneObjects`) recentred the camera every 1,000 m but shifted only kit objects, so asteroids, trails, cargo and extraction points stayed put. `Scripts/Flow/FloatingOriginGuard.cs` now turns it off in every scene: Polaris spans ±20 km, where positions are good to about 2 mm. |
