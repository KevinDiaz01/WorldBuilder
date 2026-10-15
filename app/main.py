import json, math, os, sys, tkinter as tk
from tkinter import ttk, filedialog, messagebox
from pathlib import Path
from updater import check_for_update

VERSION='0.1.1'
CELL=32
class Builder:
 def __init__(self,root):
  self.root=root; root.title(f'WorldBuilder v{VERSION}'); root.geometry('1100x750')
  self.walls=[]; self.furniture=[]; self.mode='Wall'; self.color='#d7d6d0'; self.start=None; self.path=None
  self.status=tk.StringVar(value='Draw walls: click start, then click end. Grid = 1 meter.')
  bar=ttk.Frame(root);bar.pack(fill='x',padx=8,pady=6)
  for label,fn in [('New',self.new),('Open',self.open),('Save',self.save),('Export to Unity',self.export),('Check updates',lambda:check_for_update(root,VERSION))]: ttk.Button(bar,text=label,command=fn).pack(side='left',padx=3)
  tools=ttk.Frame(root);tools.pack(fill='x',padx=8)
  ttk.Label(tools,text='Tool:').pack(side='left')
  for m in ['Wall','Floor','Couch','Table','Erase']:
   ttk.Button(tools,text=m,command=lambda m=m:self.set_mode(m)).pack(side='left',padx=3)
  ttk.Label(tools,text='Wall/floor:').pack(side='left',padx=(15,3))
  for color in ['#d7d6d0','#eeeeee','#aeb5b9','#9e8067','#30343b']:
   tk.Button(tools,bg=color,width=3,command=lambda c=color:self.set_color(c)).pack(side='left',padx=2)
  ttk.Label(root,textvariable=self.status).pack(side='bottom',fill='x',padx=10,pady=6)
  self.canvas=tk.Canvas(root,bg='#20252b',highlightthickness=0);self.canvas.pack(fill='both',expand=True,padx=8,pady=8)
  self.canvas.bind('<Button-1>',self.click);self.canvas.bind('<Motion>',self.motion);self.canvas.bind('<Configure>',lambda e:self.draw())
  self.preview=None;self.draw()
 def set_mode(self,m):self.mode=m;self.start=None;self.status.set(f'{m} selected');self.draw()
 def set_color(self,c):self.color=c;self.status.set(f'Material color: {c}')
 def snap(self,e):return (round((e.x-40)/CELL),round((e.y-40)/CELL))
 def xy(self,p):return (40+p[0]*CELL,40+p[1]*CELL)
 def click(self,e):
  p=self.snap(e)
  if self.mode in ('Wall','Floor'):
   if self.start is None:self.start=p;self.status.set('Select opposite corner/end point')
   else:
    a=self.start;self.start=None
    if a!=p:self.walls.append({'type':self.mode.lower(),'a':list(a),'b':list(p),'color':self.color})
  elif self.mode=='Erase':
   target=self.closest(p)
   if target:
    group,item=target;group.remove(item)
  else:self.furniture.append({'type':self.mode.lower(),'at':list(p),'rotation':0})
  self.draw()
 def closest(self,p):
  for item in reversed(self.furniture):
   if math.dist(item['at'],p)<1.5:return self.furniture,item
  for item in reversed(self.walls):
   a=item['a'];b=item['b'];
   if item['type']=='floor' and min(a[0],b[0])<=p[0]<=max(a[0],b[0]) and min(a[1],b[1])<=p[1]<=max(a[1],b[1]):return self.walls,item
   if item['type']=='wall':
    dx=b[0]-a[0];dy=b[1]-a[1];t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy))) if dx or dy else 0
    if math.dist(p,(a[0]+t*dx,a[1]+t*dy))<.6:return self.walls,item
  return None
 def motion(self,e):
  if self.start is not None:self.preview=self.snap(e);self.draw()
 def draw(self):
  c=self.canvas;c.delete('all');w=c.winfo_width();h=c.winfo_height()
  for x in range(40,w,CELL):c.create_line(x,0,x,h,fill='#343a40')
  for y in range(40,h,CELL):c.create_line(0,y,w,y,fill='#343a40')
  for item in self.walls:
   x1,y1=self.xy(item['a']);x2,y2=self.xy(item['b'])
   if item['type']=='wall':c.create_line(x1,y1,x2,y2,fill=item['color'],width=7,capstyle='projecting')
   else:c.create_rectangle(x1,y1,x2,y2,fill=item['color'],outline='#72777d',stipple='gray50')
  for item in self.furniture:
   x,y=self.xy(item['at']);color='#65b5bd' if item['type']=='couch' else '#cf9c65'
   c.create_rectangle(x-22,y-12,x+22,y+12,fill=color,outline='white');c.create_text(x,y,text=item['type'].title(),fill='black',font=('Arial',9))
  if self.start is not None and self.preview is not None:
   x1,y1=self.xy(self.start);x2,y2=self.xy(self.preview)
   if self.mode=='Wall':c.create_line(x1,y1,x2,y2,fill='#70e0ff',width=5,dash=(6,4))
   else:c.create_rectangle(x1,y1,x2,y2,outline='#70e0ff',dash=(6,4))
  c.create_text(12,12,anchor='nw',fill='white',text=f'WorldBuilder {VERSION} | {self.mode} | 1 square = 1 meter')
 def data(self):return {'schema':1,'app_version':VERSION,'walls':self.walls,'furniture':self.furniture}
 def new(self):self.walls=[];self.furniture=[];self.path=None;self.start=None;self.draw()
 def open(self):
  p=filedialog.askopenfilename(filetypes=[('WorldBuilder Project','*.wb.json')]);
  if not p:return
  try:
   d=json.loads(Path(p).read_text(encoding='utf-8'))
   if d.get('schema')!=1:raise ValueError('Unsupported project schema')
   self.walls=d['walls'];self.furniture=d['furniture'];self.path=p;self.draw()
  except Exception as e:messagebox.showerror('Open failed',str(e))
 def save(self):
  p=self.path or filedialog.asksaveasfilename(defaultextension='.wb.json',filetypes=[('WorldBuilder Project','*.wb.json')]);
  if p:Path(p).write_text(json.dumps(self.data(),indent=2),encoding='utf-8');self.path=p;self.status.set('Saved '+p)
 def export(self):
  p=filedialog.asksaveasfilename(title='Export scene for Unity importer',defaultextension='.json',initialfile='worldbuilder-scene.json',filetypes=[('JSON','*.json')]);
  if p:Path(p).write_text(json.dumps(self.data(),indent=2),encoding='utf-8');messagebox.showinfo('Exported','Scene exported. In Unity, install the importer C# file and use Tools > WorldBuilder > Import Scene.')
if __name__=='__main__':
 root=tk.Tk();Builder(root);root.mainloop()
