# ARXON Agent Instructions

## Core Principle

Never optimize for speed over correctness on implementation tasks.

Never infer runtime behavior from static code alone during debugging.

Evidence is always preferred over assumptions when fixing issues.

When uncertain about code changes, investigate first.

## Query Routing & Interaction Mode

- **General / Conversational Queries**: If the user is asking a casual question, giving feedback, asking for explanations, opinions, or general workflow advice, **respond directly in text**. Do NOT trigger codebase searches, file inspections, or background investigative tools unless explicitly requested.
- **Implementation / Debugging Tasks**: When the user explicitly requests code changes, bug fixes, or scene inspection, apply the structured investigation and verification phases below.

## Working Rules (Active Code & Debugging Tasks)

- Read the relevant code, scene, prefab, and project context before making assumptions.
- Separate work into Investigation, Implementation, and Validation phases.
- During Investigation, do not modify files, scenes, assets, prefabs, or project settings.
- Do not claim a root cause without evidence.
- Prefer the smallest safe change consistent with the existing architecture.
- Do not perform unrelated refactors.
- Preserve existing gameplay behavior, coroutine flow, serialized references, prefab links, and scene structure unless the task explicitly requires changes.
- Use Unity MCP for Editor, scene, hierarchy, Inspector, Console, Play Mode, runtime, and visual verification when relevant.
- Do not claim runtime or visual validation based only on code or serialized values.
- State the exact evidence used for validation.
- If Game View, runtime, Frame Debugger, device, or visual verification was not performed, say so explicitly.
- Never report "validation passed" when only compilation, active state, assigned sprite, or serialized values were checked.
- Report changed files, methods, serialized objects, scene objects, and validation steps.
- Do not overwrite or revert user changes unless explicitly requested.
- Follow existing naming, coding, and architectural patterns.
- Read `Docs/AI/ARXON_AI_DEVELOPMENT_ENVIRONMENT.md` and `Docs/AI/ARXON_TECHNICAL_CONTEXT.md` when relevant.
