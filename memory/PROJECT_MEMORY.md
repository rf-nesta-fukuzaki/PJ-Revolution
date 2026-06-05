# PJ-Revolution Project Memory

## Project Identity

- Working title: **Peak Idiots / Peak Plunder**.
- Genre: first-person co-op mountain climbing rope action.
- Core fantasy: climb from basecamp to summit while rope physics, grappling, relic carrying, unstable routes, weather, and hazards create chaotic co-op moments.
- Current main implementation area: `Assets/Sandbox/`.
- Current main playable scene: `Assets/Sandbox/Scenes/Gameplay.unity`.
- Main Stage01 docs: `docs/map-stage01/`.

## Technical Stack

- Unity: `6000.3.x` / Unity 6.3.
- Render pipeline: URP (`com.unity.render-pipelines.universal` 17.3.0).
- Input: Unity Input System.
- Networking: Netcode for GameObjects, Relay/Lobby packages present.
- UI: uGUI and TextMeshPro.
- Terrain/map tooling: Unity Editor scripts and generated assets, not manual scene YAML edits.

## High-Level Architecture

- `Assets/Sandbox/Script/System/`
  - Game flow, expedition state, spawning, terrain generation, scoring, checkpoints, return zones, shrines, services.
- `Assets/Sandbox/Script/Player/`
  - Player state, health, interaction, environmental effects.
- `Assets/Sandbox/Script/Rope/`
  - Rope simulation and player rope behavior.
- `Assets/Sandbox/Script/Climbing/`
  - Grab points and climbing-specific components.
- `Assets/Sandbox/Script/Hazard/`
  - Rockfall, ice, collapsible platforms, temple traps, damage.
- `Assets/Sandbox/Script/Relic/` and `Assets/Sandbox/Script/Item/`
  - Relic behavior, item definitions, consumable/equipment logic.
- `Assets/Sandbox/Script/Network/`
  - Network bootstrap, player spawning, sync components, voice/proximity systems.
- `Assets/Sandbox/Script/UI/`
  - HUD, result screen, shops, pause/menu, accessibility indicators.
- `Assets/Sandbox/Script/Audio/`
  - `SoundId`, sound libraries, BGM/SE manager.
- `Assets/Sandbox/Editor/`
  - Unity Editor tools (Stage01, Items, Network, Bootstrap, Offline, Build, Terrain).
  - Menu root: `Peak Plunder/` (see `PeakPlunderEditorMenus.cs`).

## Important Existing Systems

- `GameServices`
  - Static service locator for score, save, expedition, hints, weather, helicopter, ropes, spawner, cosmetics, voice chat.
- `ExpeditionManager`
  - Owns expedition phase, checkpoints, death/respawn flow, return/result flow.
- `SpawnManager` and `SpawnPoint`
  - Own relic, hazard, route, and item activation for replay variation.
- `RouteGate`
  - Opens/closes alternate routes.
- `ReviveShrine`
  - One-use revival points in the mountain.
- `PlayerRopeSystem` and `RopeManager`
  - Existing rope mechanics. Inspect before adding any new rope implementation.
- `AudioManager` and `SoundId`
  - Use enum-based sound calls. Add IDs before adding ad-hoc string sound names.

## Stage01 / Mountain01 Goal

Stage01 is a 300m x 300m mountain with 220m target height and seven zone markers:

- `Basecamp`
- `Zone1_Forest`
- `Zone2_RockySlope`
- `Zone3_CliffWall`
- `Zone4_TempleRuins`
- `Zone5_IceWall`
- `Zone6_Summit`

The experience should allow players to start at basecamp, climb through forest, rocks, cliffs, ruins, ice wall, and summit, then trigger the summit completion flow.

## Stage01 Required Hierarchy Names

These names are code-facing and must remain exact:

- `GameManager`
- `World`
- `Mountain`
- `GrappableRocks`
- `IcePatches`
- `Checkpoints`
- `RouteGates`
- `RelicSpawnPoints`
- `PlayerSpawnPoints`
- `HazardSpawnPoints`

Zone names must also remain exact:

- `Basecamp`
- `Zone1_Forest`
- `Zone2_RockySlope`
- `Zone3_CliffWall`
- `Zone4_TempleRuins`
- `Zone5_IceWall`
- `Zone6_Summit`

## Stage01 Build And Validation Flow

Use Unity on a machine with Unity `6000.3.8f1` available:

1. Open the project.
2. Run `Peak Plunder > Stage01 > Build Gameplay Scene`.
3. Confirm `Assets/Sandbox/Scenes/Gameplay.unity` is generated/updated.
4. Run or check `Peak Plunder > Stage01 > Validate Gameplay Scene`.
5. Expect `[Stage01 Validate] OK`.
6. PlayMode test: route traversal, SpawnManager, RouteGate, IcePatch, SummitGoal, checkpoint/respawn.

The current environment may not have Unity installed. If Unity cannot run, make code/doc changes and clearly mark Unity-side validation as pending.

## Stage01 Validation Targets

- `GrappableRocks` children: at least 50.
- Zone1 trees: at least 20.
- `IcePatch`: at least 12.
- `RouteGate`: at least 4.
- `ReviveShrine`: at least 6.
- `ZoneCheckpoint`: at least 5.
- Relic `SpawnPoint`: 9.
- Hazard `SpawnPoint`: 9.
- Item `SpawnPoint`: 5.
- `SummitGoal` has `SummitGoalTrigger`.
- `GameManager` has `SpawnManager`, `ExpeditionManager`, `ScoreTracker`, `AudioManager`.
- `ReturnPoint` has `ReturnZone` and `NetworkObject`.

## Implementation Rules

- Prefer modifying existing systems over adding parallel systems.
- Use Unity Editor scripts for generated scenes and large placement work.
- Do not hand-edit large `.unity` YAML sections.
- Runtime scripts must not reference `UnityEditor`.
- Editor scripts belong under `Assets/Sandbox/Editor/` or must be wrapped with `#if UNITY_EDITOR`.
- Keep tags, layers, and exact names aligned with docs and validator.
- Use primitives and generated URP/Lit materials when real art assets are missing.
- For `Grappable` objects, ensure tag/layer, collider, and kinematic rigidbody where appropriate.
- For player triggers, use `CompareTag("Player")`.
- For audio, use `PeakPlunder.Audio.SoundId` and `AudioManager`.
- For networked gameplay, check `NetworkObject`, ownership, spawn/despawn, RPC, and parent-child constraints before changing behavior.

## Known Notes

- `docs/map-stage01/MAP_06_実装メモ.md` records that Unity was not found on this PC and PlayMode validation must be done elsewhere.
- There is a duplicate root-level `MAP_06_実装メモ.md` in the working tree. Treat `docs/map-stage01/MAP_06_実装メモ.md` as the canonical docs location unless the user says otherwise.
- `memory/NGO_REMOVAL_PLAN.md` exists. Inspect it before doing large Netcode/NGO changes.
