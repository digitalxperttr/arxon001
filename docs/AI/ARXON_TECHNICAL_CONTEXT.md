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
