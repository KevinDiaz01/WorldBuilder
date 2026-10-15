# WorldBuilder 3D — Unity prototype

This is the **source prototype**, not yet a Windows installer or a v0.2.0 update.

## Open in Unity

1. Install Unity Hub and a Unity 6 LTS editor with Windows Build Support.
2. Create a **3D (Built-In Render Pipeline)** Unity project.
3. Copy this folder's `Assets/WorldBuilder` into the project's `Assets/WorldBuilder`.
4. In **Edit > Project Settings > Player > Active Input Handling**, select **Both** or **Input Manager (Old)**, because the prototype uses Unity's classic input API.
5. In the Unity menu choose **Tools > WorldBuilder > Create 3D Prototype Scene**, then press **Play**.

## Controls

- Right mouse drag: orbit
- Mouse wheel: zoom
- WASD: pan
- 1: walls (click endpoints)
- 2: floors (click opposite corners)
- 3: couch placeholder
- 4: table placeholder
- 5: erase
- Escape: cancel endpoint
- Ctrl+S / Ctrl+O: save/load
- F1: toggle help

Saved projects live at `Application.persistentDataPath/WorldBuilder-project.json` and use schema 2. The 2D Python app uses schema 1: **conversion is not implemented yet**. Keep existing 2D projects unchanged.

This is an early prototype: primitives rather than furniture models, no material browser, no VRChat export, no UI polish, and no built/tested Windows executable. It must not be distributed as an in-app update until Windows build and migration tests pass.
