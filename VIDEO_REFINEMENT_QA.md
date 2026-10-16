# WorldBuilder — Video Review Refinement Pass

Status: **Code implementation complete; Windows compilation and desktop behavior require verification.**

This pass addresses the October 16 screen recording and follow-up reports that changing Garage / Door / Window tools could appear stuck, and that imported models were oversized or missing their intended materials.

## Implemented fixes

| Area | Change | How to verify |
|---|---|---|
| Opening selection | Different Garage / Door / Window presets, fresh previews on switching, Esc cancels current placement | Select Garage then Window then Door; confirm label/highlight and outline change every time |
| Opening visibility | Hollow placement outline, subdued installed trims, pre-check wall dimensions | Aim at a suitable wall; preview must not look like a solid wall |
| Imported OBJ | Multiple material groups/submeshes, MTL color and diffuse textures | Import multi-material OBJ with PNG/JPG companions |
| Imported glTF | Per-primitive glTF material assignments and base color images on supported static models | Import an uncompressed GLB/glTF with at least two color materials |
| Model scale | Normalize newly selected model to a workable size and ground its bounding box; relative scale slider | Place an unusually large statue and verify it starts near 2 m in scale |
| Existing placed models | Selected imported object has FIT MODEL TO REAL SIZE in Details | Load a prior project with an oversized asset, select it, and repair with one click |
| Materials | Avoid model-companion textures in architectural material catalog; hide previously misclassified sculpture surfaces | Gothic statue / marble bust textures should not appear as architectural materials |
| Lighting | Remove duplicate default sun and opaque ground from build scene; rebalance environment and lamp values | Check day / sunset / night for blown highlights |
| Landscape | Softer terrain geometry, layered trees, translucent water | Place hills, trees and water in Landscape |
| Stairs | Basic grouped dark railings and posts, warmer steps | Create a staircase in Build |
| UI | Pale thin scrollbars, smaller rail labels, adaptive inspector height, persistent level indicator | Check Build, Decorate, Landscape, Details panel |
| Asset browser | WebView2 child panel embedded in WorldBuilder with fallback standalone window | Select Find Assets and try browsing/downloading |
| VRChat export | Preserve model fitting/multiple materials, trees, terrain, water and balanced light preset | Export an updated world and import through VRChat Creator Companion |
| Regression checks | Unity build tests Garage->Window->Door, OBJ and glTF materials, polygon and terrain meshes | Verify latest GitHub Actions Unity Windows job passes |

## Must still be tested on a Windows PC

- Real user-supplied model: GLB, glTF, OBJ, and ZIP are supported only for static uncompressed mesh/material features implemented in the importer. Draco, meshopt, FBX, skinned/animated meshes, WebP and complex PBR require further work.
- Test 1600x900 and smaller screen UI sizing. Embedded Win32 browser behavior can differ by Windows DPI and monitor configuration; fallback is provided.
- Open an existing saved world: large previously placed imported assets **are not silently modified**. Use FIT MODEL TO REAL SIZE if desired.
- Materials belonging to some models with unusual names might still need classification cleanup.
- Terrain still uses editable placed hill tiles; it is not yet a full sculptable landscape tool. Procedural trees and water are improved previews, not photorealistic vegetation/water.
- VRChat Unity SDK interactions, baked lighting and asset interoperability require testing in the destination Unity project.
- Validate repeated door/window/garage placement and undo/redo; the new regression smoke check does not replace testing the full editor visually.

## Feedback checklist after installing

1. In **Build**, draw a long wall. Choose Garage, then Window, then Door. Verify that each changes tool and width immediately.
2. In **Decorate**, import the problematic model from the video. If it was saved earlier, select and click **FIT MODEL TO REAL SIZE**. Confirm colors, textures and collision.
3. Switch between Day and Night. Check whether white walls retain detail.
4. Build stairs and outdoor scenery. Check whether hill, water and trees match expectations.
5. Test the browser, panel scrolling, and VRChat export.
6. Press F2 for errors and share screenshots or diagnostics for anything still broken.

Project saves and crash recovery remain in place. A successful CI build validates code compilation and the smoke tests; it does not establish real gameplay or VRChat compatibility.
