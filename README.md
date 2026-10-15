# WorldBuilder

Early Windows prototype: v0.1.0 (Python/Tkinter floor-plan editor). This is **not** yet the 3D VRChat builder.

## Update channel

The app checks the latest public GitHub Release from `KevinDiaz01/WorldBuilder`. Each update must be tagged `vX.Y.Z` and contain two release assets:

- `WorldBuilder-vX.Y.Z-update.zip`
- `WorldBuilder-vX.Y.Z-update.zip.sha256`

The ZIP contains `app/main.py` and `app/updater.py` at minimum. `Check updates` downloads and stages the update; restarting applies it. The v0.1.0 installation must have `update_config.json` configured with this repository. No release has been published yet, and Windows end-to-end update testing is still required.

## Status

The first prototype is a 2D proof of concept. Updates currently require the Python launcher; a future Windows installer/EXE is planned. Never upload personal projects or licensed assets without permission.
