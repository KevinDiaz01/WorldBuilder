# WorldBuilder: October 16 recording review

The user-supplied video is 8m23s and predates builds #173/#174.

## Observations
- 00:18–02:20: Rooms, roofs, doors, windows, stairs; early cyan opening previews were confusing.
- 02:30–04:30: Decorate, furniture importing, strong clipping and materials, external browser.
- 04:48–06:15: Hills, placeholder trees, pools, harsh lights, day/night.
- 06:30–08:23: Multi-story controls, large right-hand inspector, small position nudge tool, accidental-looking disconnected geometry.

## Existing fixes before this review
- #173 improved opening presets/previews, multi-material OBJ/glTF, scaled import previews, existing-model fitting, environment, stairs and browser embedding.
- #174 packaged standalone shaders and protected project loading; user verified the magenta rendering was repaired.
- New visual pass: a bundled, dynamically tinted gradient sky replaces the generic skybox and the flat fallback, with a safe solid-color fallback if sky material is unavailable.

## This change
- Build and Landscape now start with the duplicate bottom catalog collapsed; Decorate keeps its catalog visible. The user can open/close and switch modes without losing that choice.
- A slightly shorter catalog uses the same height for all hit regions, rail, inspector and guidance.
- Current span tools support press-drag-release **and** the existing two-click endpoint workflow; both use the same geometry code.
- Studio > Lighting gains an explicit, undoable BALANCE SCENE LIGHTS button (caps excessive point/spot intensity at 1.6 and range at 12m, preserving positions) and over-brightness guidance.
- New objects are constructed successfully before their pieces enter the saved project data.
- Regression checks cover initial workspace layout.

## Still outstanding (not claimed complete)
- A true UI Toolkit migration with image-rich palette and direct drag handles.
- Sculptable continuous terrain, realistic tree/water previews and better stairs/roof tools.
- Robust multilevel topology and persistent parametric openings; verify new drag gestures manually.
- User-machine tests for repeated openings, model materials, Windows DPI and WebView2 browser embedding.
- VRChat Creator Companion export and publish round-trip.
- Acceptance screenshots across 1600×900 and smaller laptop screens.

A successful GitHub build does not establish visual or VRChat correctness.
