# QuickStart Sample (3-minute GUI flow)

This sample describes the fastest MVP check in a fresh scene.

## Hierarchy Example

- `AvatarRoot`
  - `ClosetRoot`
    - `JacketSet`
    - `CasualSet`

## Verify

1. Open `Tools/Avatar Closet/Open Window`.
2. Set Avatar Root = `AvatarRoot`, Closet Root = `ClosetRoot`.
3. Click `Scan From Closet Root`.
4. Click `Apply`.
5. Confirm `AvatarRoot/AvatarClosetModule` exists and no duplicate is created on second Apply.

If MA is missing, the pipeline stops at Validate with a clear installation message.
