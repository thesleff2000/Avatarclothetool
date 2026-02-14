# Avatar Closet Tool

`com.yourname.avatar-closet-tool` is a non-destructive VRChat avatar closet tool based on Modular Avatar (MA).

## MVP Workflow (GUI-only)

1. In Hierarchy, create:
   - `AvatarRoot`
   - `ClosetRoot` (empty object under AvatarRoot)
   - outfit set objects as direct children of `ClosetRoot`
2. Open `Tools/Avatar Closet/Open Window`.
3. Set `Avatar Root` and `Closet Root`.
4. Click `Scan From Closet Root`.
5. Click `Apply` once. Pipeline runs in fixed order:
   - `ValidateOnly -> RepairIfNeeded -> ApplyChanges`

## Important Rules

- Modular Avatar is required.
- If MA is missing, Validate returns error and Repair/Apply do not execute.
- Source FX/Menu/Params assets are not edited directly.
- Generated data is written under `AvatarRoot/AvatarClosetModule` only.

## Local Unity Verification

1. Ensure MA is installed in project:
   - VCC: `Manage Project > Add Modular Avatar`
2. Build hierarchy:
   - `AvatarRoot`
   - `ClosetRoot` under AvatarRoot
   - at least 2 outfit GameObjects as direct children of ClosetRoot
3. In window, set Avatar Root + Closet Root and click `Scan From Closet Root`.
4. Click `Apply`.
5. Confirm:
   - `AvatarClosetModule` exists under AvatarRoot
   - MA `Parameters` component contains `ACT_SET` (int)
   - MA `Merge Animator` has a controller reference
   - Generated controller contains states per outfit set
   - Re-clicking Apply does not create duplicate module
6. Run EditMode tests in Unity Test Runner:
   - Existing 4 tests + `ApplyCreatesFxControllerAndSetParam` should pass

## Sample Guide

See `Packages/com.yourname.avatar-closet-tool/Samples~/QuickStart/README.md`.

## License

MIT. See `LICENSE`.
