"""WorldBuilder 3D launcher, updates, and previous-version restoration."""
import hashlib, io, json, shutil, subprocess, threading, tkinter as tk
import urllib.request, zipfile
from pathlib import Path
from tkinter import ttk, messagebox

HOME = Path(__file__).resolve().parent
APP = HOME / "WorldBuilder.exe"
STATE = HOME / ".worldbuilder-version"
OLD = HOME / ".3d-previous"
OLD_VERSION = HOME / ".3d-previous-version"
STAGE = HOME / ".3d-stage"
JOURNAL = HOME / ".3d-recovery.json"
FILES = ("WorldBuilder.exe", "WorldBuilder_Data", "UnityPlayer.dll",
         "UnityCrashHandler64.exe", "MonoBleedingEdge", "D3D12",
         "dstorage.dll", "dstoragecore.dll", "WorldBuilder-Launcher.pyw")
URL = "https://api.github.com/repos/KevinDiaz01/WorldBuilder/releases/latest"

def remove(p):
    if p.is_dir() and not p.is_symlink(): shutil.rmtree(p)
    elif p.exists() or p.is_symlink(): p.unlink()

def write(p, text):
    tmp = p.with_name(p.name + ".temp")
    tmp.write_text(text, encoding="utf-8")
    tmp.replace(p)

def download(url):
    request = urllib.request.Request(url, headers={"User-Agent": "WorldBuilder-3D/2"})
    with urllib.request.urlopen(request, timeout=120) as response: return response.read()

def running():
    output = subprocess.run(["tasklist", "/FI", "IMAGENAME eq WorldBuilder.exe"],
                            capture_output=True, text=True, creationflags=0x08000000)
    return "WorldBuilder.exe" in output.stdout

def valid_version(v):
    if not v or not v.strip(): return (0, 0, 0)
    p = v.lstrip("v").split(".")
    if len(p) != 3 or not all(x.isdecimal() for x in p): raise ValueError("Invalid version")
    return tuple(map(int,p))

def unpack(data):
    remove(STAGE)
    STAGE.mkdir()
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        seen = set()
        for entry in archive.infolist():
            path = Path(entry.filename.replace("\\", "/"))
            if (path.is_absolute() or ".." in path.parts or ":" in str(path)
                or not path.parts or path.parts[0] not in FILES
                or str(path) in seen):
                raise ValueError("Invalid update archive path")
            seen.add(str(path))
            dest = STAGE / path
            if entry.is_dir(): dest.mkdir(parents=True, exist_ok=True)
            else:
                dest.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entry) as src, dest.open("wb") as dst:
                    shutil.copyfileobj(src,dst)
    if not (STAGE/"WorldBuilder.exe").is_file() or not (STAGE/"WorldBuilder_Data").is_dir():
        raise ValueError("Unity release is incomplete")

def recover():
    """Restore installed version if a previous update was interrupted."""
    if not JOURNAL.exists(): return
    record = json.loads(JOURNAL.read_text(encoding="utf-8"))
    backup = HOME / record["backup"]
    for name in FILES:
        saved = backup / name
        current = HOME / name
        if saved.exists():
            remove(current)
            shutil.move(str(saved), str(current))
        elif name not in record["original"]:
            remove(current)
    if record["version"]: write(STATE,record["version"]+"\n")
    else: remove(STATE)
    remove(JOURNAL)

def install(version, data):
    if running(): raise RuntimeError("Close WorldBuilder 3D before updating.")
    recover()
    try:
        unpack(data)
        # Preserve previous working version, not the installation files in Documents.
        remove(OLD)
        OLD.mkdir()
        previous = STATE.read_text(encoding="utf-8").strip() if STATE.exists() else ""
        original = [n for n in FILES if (HOME/n).exists()]
        write(JOURNAL,json.dumps({"backup":OLD.name,"original":original,"version":previous}))
        try:
            for name in original:
                shutil.move(str(HOME/name),str(OLD/name))
            for name in FILES:
                if (STAGE/name).exists(): shutil.move(str(STAGE/name),str(HOME/name))
            if not APP.is_file() or not (HOME/"WorldBuilder_Data").is_dir():
                raise RuntimeError("Update failed validation")
            write(STATE,version+"\n")
            write(OLD_VERSION,previous+"\n")
            remove(JOURNAL)
        except Exception:
            recover()
            raise
    finally:
        remove(STAGE)

def rollback():
    if running(): raise RuntimeError("Close WorldBuilder 3D before rolling back.")
    recover()
    if not (OLD/"WorldBuilder.exe").is_file() or not (OLD/"WorldBuilder_Data").is_dir():
        raise RuntimeError("No complete previous version is available.")
    current = STATE.read_text(encoding="utf-8").strip() if STATE.exists() else ""
    previous = OLD_VERSION.read_text(encoding="utf-8").strip() if OLD_VERSION.exists() else "0.0.0"
    if not previous: previous = "0.0.0"
    next_backup = HOME/".3d-rollback-working"
    remove(next_backup)
    next_backup.mkdir()
    original = [n for n in FILES if (HOME/n).exists()]
    write(JOURNAL,json.dumps({"backup":next_backup.name,"original":original,"version":current}))
    try:
        for name in original: shutil.move(str(HOME/name),str(next_backup/name))
        # The launcher is running from this file; retain the current updater
        # rather than swapping it for one saved by an older release.
        if (next_backup/"WorldBuilder-Launcher.pyw").is_file():
            shutil.copy2(next_backup/"WorldBuilder-Launcher.pyw", HOME/"WorldBuilder-Launcher.pyw")
        for name in FILES:
            # Roll back the player, not the current updater: old launchers may
            # lack recovery features and fail to parse legacy version markers.
            if name == "WorldBuilder-Launcher.pyw": continue
            if (OLD/name).exists(): shutil.copytree(OLD/name, HOME/name) if (OLD/name).is_dir() else shutil.copy2(OLD/name,HOME/name)
        if not APP.is_file() or not (HOME/"WorldBuilder_Data").is_dir():
            raise RuntimeError("Restored application is incomplete")
        write(STATE,previous+"\n")
        remove(JOURNAL)
        remove(OLD)
        next_backup.rename(OLD)
        write(OLD_VERSION,current+"\n")
    except Exception:
        recover()
        raise

class Launcher:
    def __init__(self):
        self.root=tk.Tk()
        self.root.title("WorldBuilder 3D")
        self.root.geometry("460x290")
        self.root.resizable(False,False)
        frame=ttk.Frame(self.root,padding=22);frame.pack(fill="both",expand=True)
        ttk.Label(frame,text="WORLDBUILDER 3D",font=("Segoe UI",20,"bold")).pack(anchor="w")
        self.status=tk.StringVar(value="Ready")
        ttk.Label(frame,textvariable=self.status,wraplength=410).pack(anchor="w",pady=(8,12))
        self.go=ttk.Button(frame,text="Launch WorldBuilder 3D",command=self.launch)
        self.go.pack(fill="x",pady=3)
        self.update=ttk.Button(frame,text="Check Updates",command=self.check)
        self.update.pack(fill="x",pady=3)
        self.undo=ttk.Button(frame,text="Roll Back to Previous Version",command=self.ask_rollback)
        self.undo.pack(fill="x",pady=3)
        self.version=tk.StringVar()
        ttk.Label(frame,textvariable=self.version).pack(anchor="w",pady=8)
        self.refresh()

    def refresh(self):
        self.version.set("Installed version: "+(STATE.read_text(encoding="utf-8").strip() if STATE.exists() else "local prototype"))
        self.go.config(state="normal" if APP.is_file() else "disabled")
        self.undo.config(state="normal" if (OLD/"WorldBuilder.exe").is_file() else "disabled")

    def busy(self,on,msg):
        self.status.set(msg)
        self.update.config(state="disabled" if on else "normal")
        self.go.config(state="disabled" if on else "normal")
        self.undo.config(state="disabled" if on else "normal")
        if not on: self.refresh()

    def launch(self):
        try:
            subprocess.Popen([str(APP)],cwd=str(HOME))
            self.root.destroy()
        except OSError as err: messagebox.showerror("WorldBuilder",str(err))

    def error(self,err):
        self.busy(False,str(err))
        messagebox.showerror("WorldBuilder",str(err))

    def check(self):
        self.busy(True,"Checking for new 3D releases...")
        threading.Thread(target=self.fetch,daemon=True).start()

    def fetch(self):
        try:
            release=json.loads(download(URL))
            version=release["tag_name"].lstrip("v")
            name=f"WorldBuilder-3D-v{version}.zip"
            assets={a["name"]:a["browser_download_url"] for a in release.get("assets",[])}
            if name not in assets or name+".sha256" not in assets:
                raise ValueError("Latest release is not a compatible WorldBuilder 3D update.")
            installed=(STATE.read_text(encoding="utf-8").strip() if STATE.exists() else "") or "0.0.0"
            if valid_version(version)<=valid_version(installed):
                self.root.after(0,lambda:self.done("WorldBuilder 3D is up to date."))
            else: self.root.after(0,lambda:self.ask_update(version,assets,name))
        except Exception as err: self.root.after(0,lambda text=str(err):self.error(text))

    def ask_update(self,version,assets,name):
        self.busy(False,f"Version {version} is available.")
        if messagebox.askyesno("Update WorldBuilder",f"Install v{version}? The current version will be backed up."):
            self.busy(True,"Downloading, verifying, and backing up...")
            threading.Thread(target=self.apply,args=(version,assets,name),daemon=True).start()

    def apply(self,version,assets,name):
        try:
            data=download(assets[name])
            digest=download(assets[name+".sha256"]).decode("utf-8").split()[0].lower()
            if hashlib.sha256(data).hexdigest()!=digest: raise ValueError("Update checksum mismatch")
            install(version,data)
            self.root.after(0,lambda:self.done(f"Installed v{version}. Previous version retained."))
        except Exception as err: self.root.after(0,lambda text=str(err):self.error(text))

    def ask_rollback(self):
        if messagebox.askyesno("Roll Back WorldBuilder","Restore the previous version? Close WorldBuilder first."):
            self.busy(True,"Restoring previous version...")
            threading.Thread(target=self.restore,daemon=True).start()

    def restore(self):
        try:
            rollback()
            self.root.after(0,lambda:self.done("Previous version restored."))
        except Exception as err: self.root.after(0,lambda text=str(err):self.error(text))

    def done(self,msg):
        self.busy(False,msg)
        messagebox.showinfo("WorldBuilder",msg)

if __name__=="__main__":
    try: recover()
    except Exception as exc:
        messagebox.showerror("WorldBuilder recovery failed",str(exc))
        raise
    Launcher().root.mainloop()
