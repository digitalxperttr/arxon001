# ARXON Technical Context

This file records stable project facts for AI-assisted development. Update this technical context when the architecture changes.

## Project Identity

- ARXON is a mobile row-completion puzzle game, not match-3.
- Main modes are Classic and Adventure.
- Mystic Forge is the preview pit, but the AI environment applies to the whole ARXON project.

## Core Gameplay

- The grid is 8 cells wide and uses bottom push-up spawning.
- Preview is a single 8-cell row.
- There is no fixed three-piece hand system.
- Existing coroutine-based gameplay flow should be preserved.

## Important Scenes

- `MainMenu.unity`
- `GameScene.unity`
- `AdventureGameScene.unity`
- `AdventureMap.unity`
- `Debug_BlockTest.unity`

## Important Scripts

- `GridManager.cs`
- `Block.cs`
- `InputManager.cs`
- `ScoreManager.cs`
- `LevelManager.cs`
- `ObjectiveHUD.cs`
- `ObjectiveManager`
- `AdventureLevelGenerator.cs`
- `ProgressManager.cs`

## Special Blocks

- Fire
- Slice
- Chain
- Rock
- Ice
- Fog

Chain is a two-hit obstacle. Fire and Slice are bonus blocks.

## Core Gameplay V2 Design Rules & Principles

- **Normal Block Constraints**: Normal blocks are strictly HORIZONTAL ONLY (widths: 1, 2, 3, 4 cells). No vertical, L/T, or free-form shapes.
- **S/M/L Sprite Visual System**: To prevent stretching (sündürme) artifacts:
  - Width 1: Small (`gems5_S_<color>`)
  - Width 2: Medium (`gems5_M_<color>`)
  - Width 3 & 4: Long (`gems5_L_<color>`)
  - Colors: red, blue, purple, gold, green.
  - Dynamically resolved during spawn, preview, and post-slice.
- **Preview System**:
  - Preview displays the incoming bottom-row blocks using the S/M/L sprite pipeline at ~1:1 scale (`0.99f` / `1.0f`).
  - Blocks emerge upward from the bottom well (Mystic Well/Forge).
- **No 3-Piece Hand**: ARXON does not have a 3-piece hand mechanic. Spawning is row-push based.
- **Classic Mode Level Progression**:
  - Exclusively SCORE-BASED, not row-cleared based.
  - Unlimited level progression beyond Level 10 (`postThresholdBaseGap = 1200`, `postThresholdGapIncrease = 200`).
- **Dynamic Push / Difficulty Scaling**:
  - Push roll hierarchy: Triple -> Double -> Single row.
  - Cleared-row based minimum push:
    - Level 1-5: Standard random profile.
    - Level 6-14: 2+ cleared rows guarantee minimum 2-row push.
    - Level 15+: 2 cleared rows guarantee minimum 2-row push; 3+ cleared rows guarantee minimum 3-row push.
- **Soft-Lock Definition**: True soft-lock occurs when no valid horizontal moves exist for any playable blocks currently on the board (not merely preview placement).
- **Movement & Gravity Flow**: Avoid excessive input locking (`IsBoardBusy`) or redundant delay loops after row clear / gravity settles.

## Release & Security Gate

- Official pre-release security, data integrity, anti-cheat, and platform privacy checklist is documented in [`Docs/ARXON_RELEASE_SECURITY_CHECKLIST.md`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Docs/ARXON_RELEASE_SECURITY_CHECKLIST.md).
- Prior to store release (App Store & Google Play), all checklist items (Code Security, Asset Licensing, Build Settings, iOS/Android Privacy Manifests, Save Integrity, and the Final 30-Minute Gate) must be verified.
