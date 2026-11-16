# Dead Cargo: Game Design

Status: living document. Last updated 2026-11-16.

## 1. Pitch

Dead Cargo is a single-player space extraction game. You run a small pirate outfit from a mothership. You fly out into hostile sectors, take what you can from stations, asteroid fields, planets and wrecks, and try to get home before the Authority catches you. Whatever is in your hold when you die is gone.

The loop is inspired by crime-sandbox extraction games. The setting, name, factions and art are our own.

## 2. Pillars

1. **Cargo is physical.** Loot has mass. A full hold makes the ship slower and harder to turn. Every pickup is a trade between profit and survival.
2. **Greed against heat.** Everything profitable makes noise. The longer and louder the run, the harder the Authority responds.
3. **Ship and boots.** You fly to the target, then leave the cockpit to rob it on foot. Your ship is exposed while you are away.
4. **Home is worth protecting.** The mothership stores your wealth and grows with you. A richer mothership attracts more trouble.

## 3. Core loop

1. **Prepare** on the mothership: sell cargo, repair, fit the ship, pick a contract.
2. **Deploy** to a sector by jump.
3. **Take** cargo: heists, mining, salvage, planetary pickups.
4. **Extract**: spool the jump drive and survive until it fires.
5. **Bank or lose**: extracted cargo is banked. If the ship is destroyed, the hold is lost.

## 4. Systems

| System | Summary | Phase |
|---|---|---|
| Cargo hold | Mass-limited storage on a ship. Load reduces speed and turn rate. | 1 |
| Jettison and loose cargo | Dump cargo to escape. Dropped cargo stays in the world. | 2 |
| Heat | Actions raise heat. Heat decays when the player lies low. | 3 |
| Authority response | Heat tiers trigger escalating Authority spawns. | 4 |
| Extraction | Jump-drive spool under fire, then a run result screen. | 5 |
| Mothership hub | Sell cargo, hold credits, launch runs. Progress is saved. | 6 |
| Station heists | Dock, board on foot, crack the vault, carry loot back. | 7 |
| Mining | Extract ore from asteroids. Slow, heavy, low heat. | 8 |
| Planetary cargo | Land, collect contract cargo, launch. | 9 |
| Upgrades and crafting | Ship modules and mothership rooms built from loot. | 10 |
| Rival syndicates | AI crews that compete for targets and raid the mothership. | 11 |
| Cycles | Periodic reset to a new star cluster. Rank and cosmetics persist. | 12 |

## 5. Factions

- **The Authority.** Sector police. Patrols, interceptors and jump-point blockades. Response scales with heat.
- **Syndicates.** AI pirate crews. Rivals for loot and a threat to the mothership.
- **Civilians.** Stations, haulers and miners. Targets and witnesses.

## 6. Risk rules

- Cargo in the hold is at risk until extraction completes.
- Cargo banked on the mothership is safe from run losses, but not from raids (phase 11).
- Ship loss costs the hold and the ship's fitted modules. A basic hull is always available.

## 7. Scope

- Single-player first. All threats are AI.
- Co-op crews may follow once the single-player loop is proven. No networking code is written until then.
- PC first. Mobile controls exist in the project and are kept working where cheap.

## 8. Technology

- Unity 6 (6000.3), built-in render pipeline.
- Space Combat Kit for flight, weapons, AI, radar and vehicle enter/exit.
- Flat Kit for stylised shading.
- Dead Cargo code lives in `Assets/DeadCargo/` and extends the kit without editing kit files.
