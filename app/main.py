import json,tkinter as tk
from pathlib import Path
from tkinter import ttk,filedialog
from updater import check_for_update
VERSION='0.1.1'
root=tk.Tk();root.title('WorldBuilder v'+VERSION);root.geometry('1000x700')
canvas=tk.Canvas(root,bg='#29333a');canvas.pack(fill='both',expand=True)
walls=[];furniture=[];mode=tk.StringVar(value='Wall');start=[None];current=[None]
def redraw():
 canvas.delete('all')
 for x in range(0,1200,32):canvas.create_line(x,0,x,900,fill='#3d484f')
 for y in range(0,900,32):canvas.create_line(0,y,1200,y,fill='#3d484f')
 for item in walls:
  x,y,a,b=item['coords']
  if item['type']=='Floor':canvas.create_rectangle(x,y,a,b,fill='#9e8067')
  else:canvas.create_line(x,y,a,b,fill='#dddddd',width=8)
 for item in furniture:
  x,y=item['pos'];canvas.create_rectangle(x-25,y-15,x+25,y+15,fill='#b38e6d');canvas.create_text(x,y,text=item['type'])
def click(e):
 p=(round(e.x/32)*32,round(e.y/32)*32)
 if mode.get() in ('Wall','Floor'):
  if start[0] is None:start[0]=p
  else:
   walls.append({'type':mode.get(),'coords':[*start[0],*p]});start[0]=None
 elif mode.get()=='Erase':
  if furniture:furniture.pop()
  elif walls:walls.pop()
 else:furniture.append({'type':mode.get(),'pos':p})
 redraw()
canvas.bind('<Button-1>',click)
def save():
 path=current[0] or filedialog.asksaveasfilename(defaultextension='.json')
 if path:current[0]=path;Path(path).write_text(json.dumps({'schema':1,'walls':walls,'furniture':furniture}))
def open_project():
 path=filedialog.askopenfilename()
 if path:
  d=json.loads(Path(path).read_text());walls[:]=d.get('walls',[]);furniture[:]=d.get('furniture',[]);current[0]=path;redraw()
bar=ttk.Frame(root);bar.pack(before=canvas,fill='x')
for label,fn in [('Save',save),('Open',open_project),('Check updates',lambda:check_for_update(root,VERSION))]:ttk.Button(bar,text=label,command=fn).pack(side='left')
for m in ['Wall','Floor','Couch','Table','Erase']:ttk.Button(bar,text=m,command=lambda m=m:mode.set(m)).pack(side='left')
redraw();root.mainloop()
