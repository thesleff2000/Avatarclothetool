# Avatar Closet Tool

`com.yourname.avatar-closet-tool` is a non-destructive VRChat avatar closet tool based on Modular Avatar (MA).

## MVP Workflow (OCI-style)

1. In Hierarchy, right-click GameObject and use:
   - `Inventory 기능/메뉴 지정`
   - `Inventory 기능/옷 지정`
   - `Inventory 기능/옷 파츠 지정` or `Inventory 기능/옷 파츠 지정 해제`
2. Open `Tools/Avatar Closet/Open Window`.
3. Set `Avatar Root`.
4. Click `Apply` once.
5. Pipeline runs in fixed order:
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
   - Menu root object with `ClosetMenuRoot`
   - Outfit set objects under menu root with `ClosetOutfitSet`
   - Optional part objects under set with `ClosetOutfitPart`
3. Click `Apply` in window.
4. Confirm:
   - `AvatarClosetModule` exists under AvatarRoot
   - Re-clicking Apply does not create duplicate module
   - Invalid placement (set outside root / part outside set) produces friendly validation error
5. Run EditMode tests in Unity Test Runner:
   - Existing 4 tests + new 2 tests should pass

## Sample Guide

See `Packages/com.yourname.avatar-closet-tool/Samples~/QuickStart/README.md`.

## License

MIT. See `LICENSE`.
