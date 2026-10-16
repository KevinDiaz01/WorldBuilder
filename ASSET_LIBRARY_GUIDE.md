# WorldBuilder asset-first workflow (development build)

## Where to find assets
In **Decorate → My Assets**, the library now lists imported 3D objects, scanned materials and imported panoramic skies in a single searchable collection. Individual category tabs filter the collection. **Materials** switches to the material brush; **Skyboxes** applies an environment sky rather than placing a cube.

**Find More** launches the WorldBuilder browser. Providers: Poly Haven, Sketchfab and ambientCG. The starting tab and search are based on the requested asset category. Material requests start with ambientCG, and Skyboxes start with Poly Haven HDRIs. Sources that do not list that asset kind are marked unavailable instead of showing unrelated results. The browser deliberately remains a separate resizable window with visible tabs, Back/Forward, Downloads and Close/Esc to avoid the Windows child-window toolbar regression.

After a browser download completes, a `.worldbuilder-category` companion file records the requested category. Do not remove these sidecars before import. Files are stored under Documents/WorldBuilder Assets by provider.

## Supported assets
- **3D models:** OBJ, GLB, static glTF or ZIP containing one of those. Existing geometry, thumbnail and material limitations still apply. Imported models go into the category requested at download time, with filename-based guessing only as a fallback. A selected imported model's inspector lets you correct its library category.
- **Surface materials:** compatible PNG/JPG albedo images or texture ZIPs with recognizable albedo maps. They are listed in Materials and My Assets; choose a texture then paint a wall/floor. Some advanced PBR maps are stored but are not fully rendered in the standalone editor yet.
- **Skybox/HDRI panoramas:** equirectangular Radiance `.hdr`, or JPG/PNG. Choose **Decorate → Skyboxes** or **Studio → Landscape → Find Skyboxes** to browse; select a panorama in the library to apply it. Built-in Gradient Sky returns to the original sky. Sky selection is serialized in schema-2 JSON without breaking older projects.
- **Not yet supported:** EXR HDRIs, cubemap face archives, dynamic sky shaders from other engines, VRChat automatic SDK skybox export, proprietary game-ready materials, all 3D formats not listed above. A malformed/incomplete HDR remains unimported and is reported in F2.

## Suggested verification on Windows
1. Check the browser has three legible source tabs, a titlebar and Close/Esc on your monitor, including Windows 150% DPI.
2. Choose Trees → Find More. Download a compatible model and confirm it appears under Trees **and** My Assets, not as an unrelated material.
3. Choose Materials → Find Materials. Confirm ambientCG opens first. Download compatible texture files/ZIP and check they appear as paintable Materials.
4. Choose Skyboxes → Find Skyboxes. Download a small .hdr panorama or equirectangular JPG/PNG; confirm it appears in Skyboxes and changes the sky when clicked. Save/restart/reload to confirm persistence; then switch back to Gradient Sky.
5. Use imported model inspector to recategorize an item and confirm it moves between library filters.
6. Confirm startup loads an older saved world and all previous controls still work. Review F2 diagnostics.

## Important limit
**VRChat export still requires Unity Creator Companion / SDK review and does not yet reproduce the custom standalone-editor HDRI sky or every imported PBR material automatically.** No world is published directly to VRChat by this browser feature. Keep original models and project backups.
