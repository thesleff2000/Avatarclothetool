# MVP Spec - Avatar Closet Tool

## 1. Scope

This MVP delivers exactly three features for a non-destructive, MA-based VRChat avatar closet workflow.

1. Outfit Toggle Registration
   - User selects outfit `GameObject`(s).
   - Tool registers and manages toggle definitions for those outfits.
2. Auto Menu/Parameter Generation
   - Tool generates corresponding controls/parameters through Modular Avatar components.
   - No direct editing of original avatar FX/Menu/Params assets.
3. Repair/Rebuild
   - If materials/textures/renderers change, tool re-scans avatar state and regenerates closet module.
   - Includes broken-state detection and guided recovery.

## 2. Non-Goals (MVP Out of Scope)

- Advanced UI styling or multi-window workflow.
- Per-outfit blendshape automation.
- Full migration tools from arbitrary legacy closet systems.
- Runtime (in-game) authoring workflows.

## 3. Core Principles

- Non-destructive only.
- MA-based additive module composition only.
- Deterministic Repair/Rebuild: same input config should regenerate equivalent output module.

## 4. Functional Requirements

## 4.1 Outfit Toggle Registration

- Input
  - Avatar root (descriptor owner context).
  - One or more outfit target `GameObject`s.
  - Optional display name override.
- Behavior
  - Create/maintain an internal outfit registry config.
  - Validate duplicate names, missing objects, and invalid hierarchy references.
  - Expose toggle state intent per outfit (default on/off).
- Output
  - Persisted closet config asset/component containing outfit entries and stable IDs.

## 4.2 Auto Menu/Parameter Generation (MA-Based)

- Input
  - Outfit registry config.
  - Generation settings (parameter prefix, menu root label, sync type).
- Behavior
  - Generate MA components that represent expression menu controls and parameters.
  - Build generated module under tool-owned container objects/assets only.
  - Regeneration updates existing generated module instead of touching source avatar assets.
- Output
  - Generated MA module graph that resolves to VRChat-compatible menu/parameter behavior.

## 4.3 Repair/Rebuild

- Input
  - Existing closet config + generated module.
  - Current avatar hierarchy/material/renderer snapshot.
- Behavior
  - Detect drift/breakage (missing renderer, moved object, renamed reference, material set mismatch).
  - Re-scan target outfit bindings.
  - Rebuild generated module from latest valid bindings and config.
- Output
  - Restored closet module with diagnostic report (what broke, what was repaired, what needs manual action).

## 5. Data Model (MVP-Level)

- `ClosetConfig`
  - Avatar root reference
  - List of `OutfitEntry`
  - Generation settings
  - Last build metadata/hash
- `OutfitEntry`
  - Stable ID
  - Display name
  - Target object reference
  - Default enabled state
  - Last known renderer/material fingerprint

## 6. Acceptance Criteria (MVP)

1. User can register at least one outfit object and save config without compile/runtime errors.
2. User can generate MA-based menu/parameter module without direct modification of source FX/Menu/Params assets.
3. After renderer/material/texture drift, user can run Repair/Rebuild to recover generated closet module with actionable diagnostics.

## 7. Validation Strategy

- Compile validation
  - Unity compile success for Runtime + Editor asmdefs.
- Generation validation
  - Generated controls/parameters appear under tool-owned MA module path.
  - No writes to original avatar FX/Menu/Params assets.
- Repair validation
  - Simulate breakage by changing renderer/material linkage, then run rebuild.
  - Verify recovered module and diagnostics.
