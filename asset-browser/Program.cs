using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WorldBuilderAssetBrowser;

internal static class Program {
 [STAThread] static void Main(string[] args) {
  if (args.Length<2 || !long.TryParse(args[0],out long hwnd) || hwnd==0) return;
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  Application.Run(new BrowserWindow(new IntPtr(hwnd),args[1],args.Length>2?args[2]:"Models"));
 }
}

internal sealed class BrowserWindow : Form {
 const int GWL_STYLE=-16, WS_CHILD=0x40000000, WS_POPUP=unchecked((int)0x80000000);
 [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
 [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr hwnd);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
 [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hwnd,int x,int y,int w,int h,bool repaint);
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
 readonly IntPtr unity;
 readonly string assetsRoot;
 readonly string category;
 readonly WebView2 view=new(){Dock=DockStyle.Fill};
 readonly ToolStrip strip=new(){
  GripStyle=ToolStripGripStyle.Hidden,RenderMode=ToolStripRenderMode.Professional,
  BackColor=Color.FromArgb(245,241,251),ForeColor=Color.FromArgb(37,48,73)};
 readonly ToolStripLabel status=new("Ready"){ForeColor=Color.FromArgb(83,95,119)};
 readonly System.Collections.Generic.List<ToolStripButton> sourceButtons=new();
 readonly System.Windows.Forms.Timer layoutTimer=new(){Interval=250};
 string provider="Poly Haven";
 readonly string[] names={"Poly Haven","Sketchfab","ambientCG"};
 string[] sites=Array.Empty<string>();
 int initialTab=0;
 static string Search(string value)=>Uri.EscapeDataString(value);
 static string ModelSearch(string term)=>"https://polyhaven.com/models?q="+Search(term);
 static string SketchSearch(string term)=>"https://sketchfab.com/search?features=downloadable&type=models&q="+Search(term);
 static string AmbientSearch(string term,bool materials)=>"https://ambientcg.com/list?type="+(materials?"Material":"3DModel")+"&search="+Search(term);
 static string NormalizedCategory(string value){
  switch((value??"").Trim().ToLowerInvariant()){
   case "materials":case "material":case "surface materials":return "Materials";
   case "trees":case "tree":return "Trees";
   case "terrain":case "landscape":return "Terrain";
   case "water":return "Water";
   case "plants":case "nature":return "Plants";
   case "furniture":case "lighting":case "decor":case "kitchen":case "bathroom":
   case "outdoor":case "architecture":case "models":case "other":return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
   default:return "Models";
  }
 }
 public BrowserWindow(IntPtr unityWindow,string root,string requestedCategory){
  unity=unityWindow;assetsRoot=root;category=NormalizedCategory(requestedCategory);
  string term=category=="Models"?"":category=="Trees"?"tree":category=="Plants"?"plant":category=="Terrain"?"ground":category=="Water"?"water":category=="Materials"?"texture":category.ToLowerInvariant();
  sites=new[]{term.Length==0?"https://polyhaven.com/models":ModelSearch(term),
   term.Length==0?"https://sketchfab.com/search?features=downloadable&type=models":SketchSearch(term),
   AmbientSearch(category=="Materials"?"":term,true)};
  if(category=="Trees")sites[0]="https://polyhaven.com/models/nature/trees";
  if(category=="Plants")sites[0]="https://polyhaven.com/models/nature/plants";
  if(category=="Materials"){sites[0]="https://polyhaven.com/textures";initialTab=2;}

  Text="WorldBuilder · Find "+category+"  (Close with X or Esc)";
  // Win32 SetParent hid/clipped our toolbar on some Windows DPI arrangements.
  // Prefer a dependable titled, resizable window with visible close controls.
  FormBorderStyle=FormBorderStyle.Sizable;
  ShowInTaskbar=true;
  StartPosition=FormStartPosition.CenterScreen;
  MinimumSize=new Size(850,560);
  Size=new Size(1220,750);
  KeyPreview=true;
  KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}};
  BackColor=Color.FromArgb(246,244,251);
  var back=new ToolStripButton("‹"){ToolTipText="Back"};
  var forward=new ToolStripButton("›"){ToolTipText="Forward"};
  var reload=new ToolStripButton("↻"){ToolTipText="Reload page"};
  back.Click+=(_,_)=>{if(view.CanGoBack)view.GoBack();};
  forward.Click+=(_,_)=>{if(view.CanGoForward)view.GoForward();};
  reload.Click+=(_,_)=>view.Reload();
  strip.Items.Add(back);strip.Items.Add(forward);strip.Items.Add(reload);strip.Items.Add(new ToolStripSeparator());
  for(int i=0;i<names.Length;i++){
   int index=i;
   var btn=new ToolStripButton(names[i]){
    DisplayStyle=ToolStripItemDisplayStyle.Text,
    Font=new Font("Segoe UI Semibold",10f,FontStyle.Regular),
    ForeColor=Color.FromArgb(45,60,91),
    BackColor=Color.FromArgb(248,249,255),
    Margin=new Padding(4,5,4,5),
    Padding=new Padding(12,5,12,5)};
   btn.Click+=(_,_)=>SelectSite(index);
   sourceButtons.Add(btn);
   strip.Items.Add(btn);
  }
  strip.Items.Add(new ToolStripSeparator());
  strip.Items.Add(new ToolStripLabel("  ·  Browsing: "+category+" · compatible downloads import automatically"){
   ForeColor=Color.FromArgb(102,112,137)});
  strip.Items.Add(new ToolStripSeparator());
  var folder=new ToolStripButton("My Downloads");
  folder.Click+=(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe",$"\"{assetsRoot}\""){UseShellExecute=true});
  strip.Items.Add(folder);
  strip.Items.Add(new ToolStripSeparator());
  var close=new ToolStripButton("✕  Close");
  close.ForeColor=Color.FromArgb(162,70,85);
  close.Click+=(_,_)=>Close();
  strip.Items.Add(close);
  strip.Items.Add(status);
  Controls.Add(view);Controls.Add(strip);
  strip.Dock=DockStyle.Top;
  strip.AutoSize=false;strip.Height=55;
  strip.Font=new Font("Segoe UI",10.5f,FontStyle.Regular);
  strip.BackColor=Color.FromArgb(245,241,251);
  strip.BringToFront();
  Load+=async (_,_)=>{
   try{
    Directory.CreateDirectory(assetsRoot);
    string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorldBuilder","AssetBrowserProfile");
    Directory.CreateDirectory(profile);
    var env=await CoreWebView2Environment.CreateAsync(null,profile);
    await view.EnsureCoreWebView2Async(env);
    view.CoreWebView2.Settings.AreDevToolsEnabled=false;
    view.CoreWebView2.DownloadStarting+=HandleDownload;
    view.CoreWebView2.NewWindowRequested+=(_,args)=>{
     args.Handled=true;
     if(!string.IsNullOrWhiteSpace(args.Uri))view.CoreWebView2.Navigate(args.Uri);
    };
    view.CoreWebView2.NavigationCompleted+=(_,a)=>{if(!a.IsSuccess)status.Text="Page error: "+a.WebErrorStatus;};
    SelectSite(initialTab);
    // Keep the standard Windows titlebar so controls cannot be hidden.
    // Unity does not provide a reliable WinForms child client area here.
   }catch(Exception e){status.Text="Browser unavailable: "+e.Message;}
   Activate();
  };
  FormClosed+=(_,_)=>layoutTimer.Stop();
 }
 void SelectSite(int i){
  for(int j=0;j<sourceButtons.Count;j++){
   bool current=j==i;
   sourceButtons[j].Checked=current;
   sourceButtons[j].BackColor=current?Color.FromArgb(209,218,255):Color.FromArgb(248,249,255);
   sourceButtons[j].ForeColor=current?Color.FromArgb(34,64,146):Color.FromArgb(45,60,91);
  }
  provider=names[i];
  Text="WorldBuilder · "+category+" · "+provider+" (Esc to close)";
  if(view.CoreWebView2!=null)view.CoreWebView2.Navigate(sites[i]);
  status.Text=provider;
 }
 void HandleDownload(object? sender,CoreWebView2DownloadStartingEventArgs e){
  try{
   // Use the actual originating domain when the website redirects to a CDN.
   // Selected provider tab labels the user's intended source, never the CDN.
   string directory=Path.Combine(assetsRoot,provider);
   Directory.CreateDirectory(directory);
   string name=Path.GetFileName(e.ResultFilePath);
   if(string.IsNullOrWhiteSpace(name))name="asset.bin";
   foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
   string target=Path.Combine(directory,name);
   if(File.Exists(target))target=Path.Combine(directory,Path.GetFileNameWithoutExtension(name)+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+Path.GetExtension(name));
   e.ResultFilePath=target;
   e.Handled=true;
   status.Text="Downloading "+Path.GetFileName(target);
   e.DownloadOperation.StateChanged+=(_,_)=>{
    if(IsDisposed)return;
    try{BeginInvoke(new Action(()=>status.Text=e.DownloadOperation.State==CoreWebView2DownloadState.Completed?"Downloaded: "+Path.GetFileName(target):e.DownloadOperation.State==CoreWebView2DownloadState.Interrupted?"Download interrupted":"Downloading "+Path.GetFileName(target)));}catch(ObjectDisposedException){}
   };
  }catch(Exception ex){status.Text="Download error: "+ex.Message;}
 }
 void Attach(){
  if(!IsWindow(unity)){
   FallbackWindow("Unity window was unavailable");
   return;
  }
  try{
   var h=Handle;
   int style=GetWindowLong(h,GWL_STYLE);
   SetWindowLong(h,GWL_STYLE,(style|WS_CHILD)&~WS_POPUP);
   SetParent(h,unity);
   if(GetParent(h)!=unity){
    SetWindowLong(h,GWL_STYLE,(style|WS_POPUP)&~WS_CHILD);
    FallbackWindow("Embedded browser not supported on this display");
    return;
   }
   Position();
  }catch(Exception e){FallbackWindow("Embedded browser unavailable: "+e.Message);}
 }
 void FallbackWindow(string reason){
  FormBorderStyle=FormBorderStyle.Sizable;
  ShowInTaskbar=true;
  StartPosition=FormStartPosition.CenterScreen;
  Size=new Size(1200,740);
  status.Text=reason+" · showing separate window";
 }
 void Position(){
  if(FormBorderStyle!=FormBorderStyle.None)return;
  if(!IsWindow(unity)){Close();return;}
  if(!GetClientRect(unity,out var rect))return;
  int width=Math.Max(480,rect.Right-rect.Left-149);
  int height=Math.Max(280,rect.Bottom-rect.Top-106);
  MoveWindow(Handle,134,91,width,height,true);
 }
}
