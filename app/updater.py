import hashlib,json,io,urllib.request,zipfile,shutil
from pathlib import Path
from tkinter import messagebox
BASE=Path(__file__).resolve().parent.parent
def check_for_update(root,current):
 try:
  repo=json.loads((BASE/'update_config.json').read_text())['github_repo']
  req=urllib.request.Request('https://api.github.com/repos/'+repo+'/releases/latest',headers={'User-Agent':'WorldBuilder'})
  with urllib.request.urlopen(req,timeout=15) as r:release=json.load(r)
  version=release['tag_name'].lstrip('v')
  if tuple(map(int,version.split('.')))<=tuple(map(int,current.split('.'))):messagebox.showinfo('Updates','Up to date');return
  if not messagebox.askyesno('Update',f'Install v{version} on next launch?'):return
  assets={a['name']:a['browser_download_url'] for a in release['assets']}
  name=f'WorldBuilder-v{version}-update.zip'
  def get(n):
   with urllib.request.urlopen(urllib.request.Request(assets[n],headers={'User-Agent':'WorldBuilder'}),timeout=60) as r:return r.read()
  data=get(name);digest=get(name+'.sha256').decode().split()[0]
  if hashlib.sha256(data).hexdigest()!=digest:raise ValueError('Checksum mismatch')
  temp=BASE/'.update-staged.tmp';staged=BASE/'.update-staged'
  if temp.exists():shutil.rmtree(temp)
  temp.mkdir()
  with zipfile.ZipFile(io.BytesIO(data)) as z:
   for info in z.infolist():
    dest=(temp/info.filename).resolve()
    if not dest.is_relative_to(temp.resolve()):raise ValueError('Invalid ZIP path')
    if info.is_dir():dest.mkdir(parents=True,exist_ok=True);continue
    dest.parent.mkdir(parents=True,exist_ok=True)
    with z.open(info) as src,dest.open('wb') as out:shutil.copyfileobj(src,out)
  if not (temp/'app'/'main.py').exists():raise ValueError('Invalid update')
  if staged.exists():shutil.rmtree(staged)
  temp.rename(staged);messagebox.showinfo('Update ready','Restart WorldBuilder to install.')
 except Exception as e:messagebox.showerror('Update failed',str(e))
