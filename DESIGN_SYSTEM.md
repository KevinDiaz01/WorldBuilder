# WorldBuilder Visual Model — Build-Game UX Contract

This file is a standing design reference for the **upcoming** aesthetic/UI overhaul. It documents the intended appearance, not a claim that the current Unity OnGUI editor already looks this way.

## Inspiration and product goal

The user wants a building editor that feels inviting and tactile like the construction, furnishing, and landscaping modes of The Sims 4 and inZOI, while remaining an original WorldBuilder product. The Oct 16 reference shows a bright leafy home scene with most of the screen devoted to the actual world and an image-rich, slim bottom landscaping catalog.

WorldBuilder should feel like **building a home in a game**, not operating a professional CAD application. Keep precise editing available progressively without exposing technical parameters by default.

## Screen regions at the default 1600x900 view

- **Top command ribbon** (~68px): WorldBuilder wordmark, Build / Decorate / Landscape / Live mode tabs, compact Undo/Redo, Save, Studio, and Menu.
- **Left tool rail** (~95px, collapsible): show only tools relevant to the current mode. Use comfortable icon+text tap targets, active state, hover feedback. Long tools belong behind simple sections.
- **Center viewport** (largest possible region): vivid outdoor sky/landscape and readable geometry; avoid permanently floating panels over the work area. Place gizmos and previews in-world; make intersections visibly clear.
- **Bottom catalog tray** (collapsible): visual object thumbnails, category tabs, search, free-download import guidance, and prominent selected states. Terrain mode should show grass, paths, trees, water, flowers as image-forward tiles; Build mode shows building primitives and roof shapes.
- **Right properties tray** (collapsed by default when not selecting): context-specific controls. A selected wall gets width/height/opening/finish, furniture gets location/rotation/scale and material, a light gets intensity/range/color; no unrelated properties.
- **Modal surfaces**: crash recovery, exit confirmation, and destructive workflows own focus and offer distinct primary/secondary actions.

## Style tokens / implementation guidance

- Priority: **comfortable and bright during daylight building**, with readable translucent controls over any sky. Avoid pure white panels over clouds and pure black panels over landscape.
- Base surfaces: gently frosted cool-white, pale gray/lilac, and muted charcoal for high-contrast overlays. Avoid saturated color everywhere.
- Accent: one calming blue/teal active-state color. Reserve orange/red for warnings and errors; green for safe success.
- Corner radii: roughly 12–20 px for trays/cards, 10–12 px for buttons.
- Spacing: 8 px minimum between actionable elements, 16–24 px for section separation.
- Type: Segoe UI or another installed sans serif; clear size hierarchy. Core buttons > 14 px at base scale.
- Thumbnail cards: actual imported model image when available, name, type, import status; texture swatches should show imagery.
- Live Preview: minimal HUD, prioritize immersion; F2 diagnostics only when invoked.
- Progressively reveal options: mode -> tool -> selected object -> advanced inspector instead of displaying everything at once.
- Do not rely only on color to convey selection, error, progress, or mode.

## Build / Decorate / Landscape / Live interactions

1. **Build**: focus on walls, freeform rooms, structural openings, roofs, stairs, levels, dimensions. Show snapping, measurements, and wall previews near cursor.
2. **Decorate**: image-first furniture library; translucent real model preview, floor/wall alignment, repeat placement, rotation hints and grab handles.
3. **Landscape**: terrain editing, paths, water, grass, trees and flowers. Adopt an easy brush/palette interaction with clear footprint. Avoid putting a technical inspector between the user and painting.
4. **Live**: low-distraction walkthrough and VRChat scale preview.

## Recovery UX

When a session ended unexpectedly, show a centered, calm, unambiguous **Recover your last build?** card on launch. Include project name, saved time, object count, a dominant **Restore My Build** button, and a secondary **Open My Last Saved Project** choice. Explain that restored work will not overwrite the manual save until Save is chosen. Keep recent crash and per-project autosaves available later from Projects.

## Quality bar for aesthetic-overhaul acceptance

- Screenshots of the 4 modes at 1600x900 and laptop scale show clear, consistent hierarchy.
- No tool panels overlap the library or block placing geometry.
- The bottom catalog handles long filenames and real thumbnails without clipping.
- All core actions have visible feedback and accessible keyboard shortcuts.
- A crash recovery prompt appears on real unclean exit with unsaved changes and offers a non-destructive restore.
- WorldBuilder must compile and functional tests must pass after each UI-focused change; visuals alone do not establish correctness.

Avoid copying The Sims/inZOI branded art or exact proprietary UI elements; their presentation is a reference, not a template.
