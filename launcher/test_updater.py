"""Non-GUI smoke test of WorldBuilder 3D update, rollback, and corrupted ZIP protection."""
import importlib.machinery
import io
import sys
import types
import tempfile
import zipfile
from pathlib import Path

# The updater engine is tested without opening a GUI. Linux CI may lack tkinter.
try:
    import tkinter
except ImportError:
    tkinter = types.ModuleType("tkinter")
    tkinter.ttk = types.ModuleType("tkinter.ttk")
    tkinter.messagebox = types.ModuleType("tkinter.messagebox")
    sys.modules["tkinter"] = tkinter
    sys.modules["tkinter.ttk"] = tkinter.ttk
    sys.modules["tkinter.messagebox"] = tkinter.messagebox

source = Path(__file__).with_name("WorldBuilder-Launcher.pyw")
loader = importlib.machinery.SourceFileLoader("worldbuilder_launcher_test",str(source))
mod = loader.load_module()
mod.running = lambda: False

def bundle(version, include_data=True):
    output = io.BytesIO()
    with zipfile.ZipFile(output,"w") as archive:
        archive.writestr("WorldBuilder.exe",version)
        if include_data:
            archive.writestr("WorldBuilder_Data/globalgamemanagers",version)
        archive.writestr("WorldBuilder-Launcher.pyw","launcher-"+version)
    return output.getvalue()

with tempfile.TemporaryDirectory() as tmp:
    root = Path(tmp)
    mod.HOME = root
    for name,value in {"APP":"WorldBuilder.exe","STATE":".worldbuilder-version",
                       "OLD":".3d-previous","OLD_VERSION":".3d-previous-version",
                       "STAGE":".3d-stage","JOURNAL":".3d-recovery.json"}.items():
        setattr(mod,name,root/value)
    (root/"WorldBuilder.exe").write_text("v1")
    (root/"WorldBuilder_Data").mkdir()
    (root/"WorldBuilder_Data"/"globalgamemanagers").write_text("v1")
    (root/"WorldBuilder-Launcher.pyw").write_text("launcher-v1")
    mod.STATE.write_text("0.2.0")
    mod.install("0.3.1",bundle("v2"))
    assert (root/"WorldBuilder.exe").read_text()=="v2"
    assert (mod.OLD/"WorldBuilder.exe").read_text()=="v1"
    assert mod.STATE.read_text().strip()=="0.3.1"
    assert (root/"WorldBuilder-Launcher.pyw").read_text()=="launcher-v2"
    mod.rollback()
    assert (root/"WorldBuilder.exe").read_text()=="v1"
    assert mod.STATE.read_text().strip()=="0.2.0"
    assert (mod.OLD/"WorldBuilder.exe").read_text()=="v2"
    assert (root/"WorldBuilder-Launcher.pyw").read_text()=="launcher-v2"
    try:
        mod.install("0.3.2",bundle("broken",include_data=False))
        raise AssertionError("Incomplete archive accepted")
    except ValueError:
        pass
    assert (root/"WorldBuilder.exe").read_text()=="v1"
    assert mod.STATE.read_text().strip()=="0.2.0"
    mod.OLD_VERSION.write_text("")
    mod.rollback()
    assert mod.STATE.read_text().strip()=="0.0.0"
    assert mod.valid_version("") == (0,0,0)
print("WorldBuilder update/rollback tests passed.")
