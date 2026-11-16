# Dead Cargo: Polish Backlog

Issues found in testing that were deferred. Review this list before each milestone closes.

| # | Found | Phase | Issue | Notes |
|---|---|---|---|---|
| 1 | 2026-11-16 | 2 | Jettisoned crates do not visibly drift apart | Likely cause: `Scatter Speed` (2 m/s) with crate damping 0.5 gives about 4 m of travel, close to the 3 m crate size. Try a higher scatter speed, lower damping, or spawn offsets per crate. |
| 2 | 2026-11-16 | 1, 3 | Delete the superseded `Scripts/UI/CargoHudPanel.cs` and `Scripts/UI/HeatHudPanel.cs` | No scene or prefab uses them any more, and all code references were removed on 2026-11-16. Only the two files (and their `.meta`) are left. The deletion waits on the user's approval. |
