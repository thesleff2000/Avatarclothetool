# QuickStart Sample (3-minute flow)

This sample describes the fastest MVP check in a fresh scene.

## Hierarchy Example

- `AvatarRoot`
  - `ClosetMenu` (assign `Inventory 기능/메뉴 지정`)
    - `JacketSet` (assign `Inventory 기능/옷 지정`)
      - `Hood` (assign `Inventory 기능/옷 파츠 지정`)
    - `CasualSet` (assign `Inventory 기능/옷 지정`)
      - `Sleeve` (assign `Inventory 기능/옷 파츠 지정`)

## Verify

1. Open `Tools/Avatar Closet/Open Window`.
2. Set Avatar Root = `AvatarRoot`.
3. Click `Apply`.
4. Confirm `AvatarRoot/AvatarClosetModule` exists.
5. Click `Apply` again and confirm no duplicate module is created.

If MA is missing, the pipeline stops at Validate with a clear installation message.
