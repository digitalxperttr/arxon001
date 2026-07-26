# ARXON AI Development Environment

## Purpose

The ARXON AI development environment defines how AI-assisted work is investigated, planned, implemented, validated, and reported across the project. It exists to keep changes evidence-based, small, reversible, and aligned with existing Unity architecture and gameplay behavior.

This environment applies to the whole ARXON project. It is not a Mystic Forge-specific setup.

## Roles

- ChatGPT helps reason about goals, constraints, architecture, validation standards, and communication.
- Codex reads and edits repository files, performs implementation work, and reports changed files and validation evidence.
- Unity MCP provides Editor, scene, hierarchy, Inspector, Console, Play Mode, runtime, Game View, and visual verification access when relevant.
- The developer defines product intent, accepts changes, performs final playtests when needed, and confirms device-specific behavior.

## Work Phases

### Investigation

Read the relevant code, scenes, prefabs, serialized references, project settings, logs, and documentation before making assumptions. Do not modify files, scenes, assets, prefabs, or project settings during this phase.

### Planning

Describe the intended change, the affected systems, the smallest safe approach, expected risks, and required evidence. Plans should respect existing naming, coding, coroutine, scene, prefab, and architecture patterns.

### Implementation

Make only the changes required for the task. Avoid unrelated refactors. Preserve gameplay behavior, coroutine flow, serialized references, prefab links, and scene structure unless the task explicitly requires changing them.

### Validation

Validate at the evidence level required by the task. Runtime or visual claims require runtime or visual evidence. Compilation, active state, assigned sprites, and serialized values are not enough to claim full validation.

### Acceptance

Report exactly what changed, what was validated, what evidence was used, and what was not verified. The developer accepts the change after reviewing the report and performing any required playtest or device verification.

## Evidence Levels

1. Code: source files, methods, logic, compiler output, static references.
2. Serialized scene/prefab/Inspector: scene objects, prefab links, serialized fields, Inspector state.
3. Runtime state: Play Mode behavior, Console output, runtime object state, coroutine execution.
4. Game View or capture: visual confirmation through Game View, screenshots, capture, Frame Debugger, or equivalent rendered evidence.
5. Developer playtest/device verification: hands-on testing by the developer, especially for touch controls, device layout, performance, and feel.

## MCP Usage Principles

- Use Unity MCP when Editor, scene, hierarchy, Inspector, Console, Play Mode, runtime, or visual verification is relevant.
- Prefer direct Unity evidence over assumptions from code or serialized values.
- Do not claim runtime behavior unless Play Mode or runtime state was inspected.
- Do not claim visual correctness unless Game View, capture, Frame Debugger, or device evidence was inspected.
- State when MCP, Play Mode, Game View, Frame Debugger, device, or visual verification was not performed.

## Validation Reporting Standards

Every validation report should state:

- The exact evidence used.
- The files, methods, serialized objects, and scene objects checked.
- Whether compilation, tests, Console, Play Mode, Game View, capture, Frame Debugger, or device playtest were performed.
- Any validation gaps or unverified assumptions.

Never report "validation passed" when only compilation, active state, assigned sprite, or serialized values were checked.

## Minimal-Change Principle

Prefer the smallest safe change consistent with the existing architecture. Avoid unrelated refactors, broad rewrites, hidden behavior changes, and scene or prefab edits that are not required by the task.

## Standard Task Template

### Context

Relevant feature, scene, scripts, prefabs, serialized references, known constraints, and current behavior.

### Goal

The exact outcome requested.

### Constraints

Files, systems, scenes, prefabs, behavior, references, coroutine flow, or architecture that must be preserved.

### Evidence Required

The required evidence level for acceptance, from code through developer playtest/device verification.

### Validation

Specific checks to perform, including Unity MCP, Console, Play Mode, Game View, capture, Frame Debugger, or device testing when relevant.

### Report

Changed files, changed methods, changed serialized objects, changed scene objects, validation steps, exact evidence, and any verification not performed.
