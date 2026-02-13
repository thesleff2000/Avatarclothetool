# AGENTS Guide - Avatar Closet Tool

## Tool Purpose

- Target: VRChat avatar closet automation tool.
- Architecture principle: non-destructive workflow.
- Generation strategy: Modular Avatar (MA)-based module composition.
- Maintenance strategy: Repair-Rebuild flow for deterministic regeneration.

## Coding Conventions

- C# namespace:
  - Base namespace: `YourName.AvatarClosetTool`.
  - Runtime namespace: `YourName.AvatarClosetTool.Runtime`.
  - Editor namespace: `YourName.AvatarClosetTool.Editor`.
- File naming:
  - One public type per file.
  - File name must match type name.
  - Suffixes:
    - Editor-only helpers: `*Editor`, `*Menu`, `*Window`
    - Data/config objects: `*Config`, `*Settings`

## Frequent Validation Procedure

1. Compile check
   - Unity Console has no C# compile errors.
   - `Runtime` and `Editor` asmdef assets resolve correctly.
2. Menu check
   - `Tools/Avatar Closet Tool/...` menu entries appear.
   - Menu action executes without exceptions.
3. Parameter/menu generation checklist
   - Generated parameters are present and named as expected.
   - Generated menu controls are present and correctly linked.
   - Re-running generation is idempotent or safely repairable.
4. Repair-Rebuild check
   - Broken/generated modules can be rebuilt from source settings.
   - Rebuild does not require manual edits on generated assets.

## Never Do This

- Never directly edit original avatar FX/Menu/Params assets.
- Always use module-additive composition (MA/module injection approach).
