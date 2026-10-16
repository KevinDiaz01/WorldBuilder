# WorldBuilder 1.0 — Feature Preview and Release Checklist

WorldBuilder is a Windows Unity editor-style world-building tool that exports geometry to a Unity project for VRChat. This branch is a **feature preview**, not a fully tested finished product. GitHub Actions green means Unity compilation/build passed; it does not prove every feature or exported VRChat SDK interaction works.

## In this all-in-one feature-preview update

- **Architecture** — Rectangular rooms, walls, levels, stairs, roofs and custom polygon-room outlines. Select Rooms, press C for Custom, click three or more corners, and press Enter to close. Backspace removes the last corner. Avoid intersecting outlines.
- **Wall openings** — Studio > Architecture offers opening width, height, bottom offset, and garage-sized entrances. These are split-wall gaps, not yet editable Boolean cutting volumes.
- **Models** — Browser providers Poly Haven, Sketchfab and ambientCG. The importer handles OBJ, GLB, glTF and compatible ZIPs. Sketchfab downloads are subject to login, download availability and each model's license. Unity packages, Blender and FBX remain incompatible.
- **Lighting** — Studio > Lighting offers point and spot lights, brightness and range. The exporter places native Unity lights. Baked lighting generation is not yet provided.
- **Environment** — Studio > Landscape provides procedural terrain tiles, water surfaces, fog and day/sunset/night/overcast sky-color presets. Drag on the ground to create terrain or water tiles. Not yet a Unity Terrain sculpt/paint system.
- **VRChat** — Studio > VRChat includes mirror, seat/station, and spawn markers. The Unity exporter attempts SDK components if available. Compatibility needs testing in VRChat Creator Companion, especially mirror and seat types.
- **Editor** — Existing asset library, material painter, snapping, inspector, F2 console, autosave recovery, history foundation and exporter remain integrated.

## Still required before WorldBuilder 1.0 can honestly be called complete

1. Verify successful Windows build and editor boot; repair compiler/runtime regressions.
2. Test concave polygon rooms, floors and collisions in the exported scene.
3. Import a realistic free Sketchfab GLB/glTF model and confirm texture, scale, rotation, save/reload and VRChat export.
4. Verify lighting behavior, shadows, performance and baking in the target VRChat-compatible Unity version.
5. Implement sculpting brushes, terrain material painting, roads, foliage, skybox images and atmosphere.
6. Upgrade openings to nondestructive, draggable, persistent parametric cutouts; improve roofs and multilevel architecture.
7. Validate mirror, station, spawn and SDK types; implement Udon-prefab integration, interactive doors, switches, teleports and video players.
8. Add proper draggable 3D gizmos, multi-selection, grouping, collision-aware placement, and comprehensive undo/redo.
9. Add resource budgets, diagnostics, optimizer, crash-safe persistence, migrations and automated test worlds.
10. Provide tutorial, templates and a documented, end-to-end VRChat publish walkthrough.

## Acceptance criteria

The software is complete when a user can create, save, reopen, furnish and landscape a custom-shaped two-story property with a garage; configure day/night lighting and VRChat interactions; export and successfully publish the result to VRChat at reasonable PCVR performance.

## Controls

- F2 — diagnostic console
- C while Rooms selected — rectangular/custom polygon room mode
- Enter in polygon mode — close room
- Backspace in polygon mode — remove most recent corner
- N — furniture snapping toggle
- Q/E/R during imported furniture placement — rotation
- Ctrl+S — save
- Studio top-bar button — Architecture, Lighting, Landscape, VRChat

Publishing still uses VRChat Creator Companion and the VRChat Worlds SDK.
