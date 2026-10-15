# Publishing WorldBuilder v0.1.1

Create a GitHub Release at https://github.com/KevinDiaz01/WorldBuilder/releases/new with a new tag `v0.1.1` targeting `main`. Attach the two release assets **exactly** named:

- `WorldBuilder-v0.1.1-update.zip`
- `WorldBuilder-v0.1.1-update.zip.sha256`

Publish as latest release (not a pre-release). The existing v0.1.0 application checks `/releases/latest` and needs both assets. Do not rename them.

On Windows: open WorldBuilder v0.1.0, click **Check updates**, accept, close and reopen via desktop shortcut. Confirm title says v0.1.1 and saved project still opens. This smoke test has not yet been completed. The SHA-256 file is an integrity check, not a signature/authenticity guarantee.
