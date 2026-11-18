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
| Authority ships out of the sector | 2026-11-17 | While the player is away in cruise (phase 6.12), Authority ships already hunting sleep with the sector, but new ones may still spawn near the player at a planet, and they do not follow when the ship is moved back to the world's origin every 20 km. Decide whether hunters follow into cruise, wait at the sector, or give up. |
| Planet ground in front of near objects | 2026-11-17 | A planet is drawn by the far camera, behind the near world (phase 6.12 step 2), so a hill cannot hide a ship behind it and the player's ship casts no shadow on the ground. A depth pass from the far picture into the main camera would let terrain hide near objects; a simple contact shadow under the ship would ground it. |
| Water you can float in | 2026-11-17 | Ixara's sea is a solid sphere at sea level for now. Floating, and going under, need buoyancy and Planet Forge's underwater effects, which were left out of the planet copies. |
| Trees and rocks on planets | 2026-11-17 | Planet Forge's demo planets place trees, grass and boulders with spawners that the catalogue builder strips. Bring them back on the far copy, scaled for 1:1000, with the planet look (phase 6.12 step 3). |
| Remove the old star sky | 2026-11-16 | `Materials/DeepSpaceSkybox.mat` (phase 6.7a) is unused once the space sky passes its test. Delete it then. |

## Resolved

| # | Found | Phase | Issue | Resolution |
|---|---|---|---|---|
| 1 | 2026-11-16 | 2 | Jettisoned crates do not visibly drift apart | Fixed 2026-11-16. The sideways scatter used `Random.insideUnitSphere`, which often gave almost no speed, and Jettison All spawned every crate at the same point. Each crate now gets the full scatter speed at right angles to its exit, and Jettison All fans crates out around the ejection point (`Dump Spacing`, 4 m). |
| 2 | 2026-11-16 | 1, 3 | Delete the superseded `CargoHudPanel` and `HeatHudPanel` scripts | Deleted 2026-11-16, after no scene, prefab or code used them. |
| 3 | 2026-11-16 | 6.10 | The ship seems to jump back towards the start after flying about 1 km from the sector's centre | Fixed 2026-11-16. The kit's Floating Origin Manager (in `SceneObjects`) recentred the camera every 1,000 m but shifted only kit objects, so asteroids, trails, cargo and extraction points stayed put. `Scripts/Flow/FloatingOriginGuard.cs` now turns it off in every scene: Polaris spans ±20 km, where positions are good to about 2 mm. |
| 4 | 2026-11-17 | 6.12 | Cruise engaged inside the belt and flew through it, dropping out on a hit; a mass lock would read better | Done 2026-11-17 in step 2b, as the user asked: the drive will not engage while an asteroid field lies along the route out of the sector (BLOCKED  FIELD), checked against the field density every 50 m. |
| 5 | 2026-11-17 | 6.12 | Near a planet the ship kept rolling itself towards a tilted horizon | Fixed 2026-11-17. The kit's auto roll levels to the world's up, which over a planet is tilted against the ground. Level flight (`LevelFlightInput`) replaces it, levelling to the planet's horizon within its reach and the sector's up in open space; Set Up Sector switches the kit's auto roll off. |
| 6 | 2026-11-16 | 6.8, 6.10 | Fast ships may pass through small rocks: kit ships use discrete collision, so at full boost a ship could end up inside a field rock or pass through a small trail rock | Done 2026-11-18 in phase 6.16 step 1: the player's ship uses continuous speculative collision, set by Set Up Sector. Tested 2026-11-18 |
| 7 | 2026-11-18 | 6.6 | Every Play session ended with `Leak Detected : Persistent allocates 1 individual allocations` in Editor.log | Fixed and tested 2026-11-18. The stack trace named Planet Forge: `SgtLandscape.HeightData.Create`, from `SgtLandscapeTextures.GenerateAtlas`. SGT caches each landscape texture's height data in a static store and frees an entry only at a later rebuild, judged against the textures in use one rebuild before. So the last planet's height data outlived it, held memory between runs, and was reported at the next domain reload. SGT is never edited. `Sectors/Far/PlanetTextureCache.cs` runs one more `SgtLandscapeTextures.RebuildAll` once no landscape is left: after each scene unloads, and as the editor leaves Play Mode (`Editor/PlanetTextureCacheRelease.cs`). After the fix, a full session (mothership, then Polaris with its planets) left nothing for the next reload to report. |
