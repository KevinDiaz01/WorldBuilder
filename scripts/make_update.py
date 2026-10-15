import hashlib,sys,zipfile
from pathlib import Path
version,root=sys.argv[1],Path(sys.argv[2])
out=Path.cwd()/f'WorldBuilder-v{version}-update.zip'
with zipfile.ZipFile(out,'w',compression=zipfile.ZIP_DEFLATED) as z:
 for f in (root/'app').rglob('*.py'):z.write(f,f.relative_to(root))
(out.parent/(out.name+'.sha256')).write_text(hashlib.sha256(out.read_bytes()).hexdigest()+'  '+out.name+'\n')
