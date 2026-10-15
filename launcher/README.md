# WorldBuilder 3D updates and recovery

The installed WorldBuilder 3D shortcut points to WorldBuilder-Launcher.pyw, stored beside WorldBuilder.exe. The launcher downloads only 3D releases named WorldBuilder-3D-vX.Y.Z.zip with a matching .sha256. Releases replace the Unity Windows player, not Unity project source.

**Publishing:** Commits to unity/ or launcher/ on main start the Build and release WorldBuilder 3D workflow. The build verifies the launcher backup/rollback tests and compiles Windows Unity; only successful builds publish a new v0.3.N release. Manual workflow dispatch with a blank version remains build-only.

**Restore:** Before installation the launcher retains the current application components in .3d-previous. Use **Roll Back to Previous Version** in the launcher if a newer version is broken. The launcher retains the last two versions (current and previous); updating again replaces the older backup. Interrupted installs are recovered using .3d-recovery.json when the launcher starts. Keep both application and backup folders on the same drive. Projects are not deleted or moved: Unity saves them separately in its persistentDataPath.

**Limitations:** The rollback command restores installed application components, not saved project contents or incompatible data schemas. If the launcher itself cannot run, recovery may require manual restoration from .3d-previous. Publishing is automatically triggered after a source commit; it is not triggered merely by talking about an update in ChatGPT.
