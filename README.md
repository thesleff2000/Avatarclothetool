# Avatar Closet Tool (Unity Package Skeleton)

`com.yourname.avatar-closet-tool` is a distributable Unity package skeleton for automating VRChat avatar outfit workflows.

## Goal

- Build a non-destructive avatar closet automation tool.
- Keep runtime and editor implementation separated.
- Provide a safe foundation for MA-based generation and repair/rebuild workflows.

## Repository Structure

```text
Packages/
  com.yourname.avatar-closet-tool/
    package.json
    Runtime/
      com.yourname.avatar-closet-tool.Runtime.asmdef
      AvatarClosetRuntimeMarker.cs
    Editor/
      com.yourname.avatar-closet-tool.Editor.asmdef
      AvatarClosetToolMenu.cs
README.md
AGENTS.md
LICENSE
```

## Development Notes

- Runtime code lives under `Runtime/`.
- Unity editor-only code lives under `Editor/`.
- Editor assembly references Runtime assembly.
- Current editor menu placeholder:
  - `Tools > Avatar Closet Tool > Open Placeholder Window`

## How To Use In Unity

1. Open a Unity project.
2. Place this package at `Packages/com.yourname.avatar-closet-tool`.
3. Let Unity reimport scripts.
4. Confirm there are no compile errors.
5. Use the placeholder menu to verify editor assembly load.

## License

MIT. See `LICENSE`.
