# Dead Cargo: Polish Backlog

Issues found in testing that were deferred. Review this list before each milestone closes.

| # | Found | Phase | Issue | Notes |
|---|---|---|---|---|
| 1 | 2026-11-16 | 2 | Jettisoned crates do not visibly drift apart | Likely cause: `Scatter Speed` (2 m/s) with crate damping 0.5 gives about 4 m of travel, close to the 3 m crate size. Try a higher scatter speed, lower damping, or spawn offsets per crate. |
| 2 | 2026-11-16 | 1, 3 | Delete the superseded `CargoHudPanel` and `HeatHudPanel` scripts | Do this once the range has been tidied and saved as the template, so no scene still holds them. Then remove `ExtractionHud.SuppressPhasePanels` and `SceneHierarchy.RemoveLegacyObjects`. |
