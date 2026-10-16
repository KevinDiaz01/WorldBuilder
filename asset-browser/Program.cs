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
  Application.Run(new BrowserWindow(new IntPtr(hwnd),args[1]));
 }
}

internal sealed class BrowserWindow : Form {
 const int GWL_STYLE=-16, WS_CHILD=0x40000000, WS_POPUP=unchecked((int)0x80000000);
 [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
 [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hwnd,int x,int y,int w,int h,bool repaint);
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
 readonly IntPtr unity;
 readonly string assetsRoot;
 readonly WebView2 view=new(){Dock=DockStyle.Fill};
 readonly ToolStrip strip=new(){GripStyle=ToolStripGripStyle.Hidden,RenderMode=ToolStripRenderMode.System};
 readonly ToolStripLabel status=new("Ready");
 readonly System.Windows.Forms.Timer layoutTimer=new(){Interval=250};
 string provider="Poly Haven";
 readonly string[] names={"Poly Haven","Unity Asset Store","ambientCG"};
 readonly string[] sites={"https://polyhaven.com/models","https://assetstore.unity.com/3d","https://ambientcg.com/list?type=Material"};
 public BrowserWindow(IntPtr unityWindow,string root){
  unity=unityWindow;assetsRoot=root;
  Text="WorldBuilder Assets Browser";
  FormBorderStyle=FormBorderStyle.Sizable;
  ShowInTaskbar=true;
  StartPosition=FormStartPosition.CenterScreen;
  MinimumSize=new Size(800,520);
  Size=new Size(1220,750);
  BackColor=Color.FromArgb(26,32,40);
  var back=new ToolStripButton("←");
  var forward=new ToolStripButton("→");
  var reload=new ToolStripButton("↻");
  back.Click+=(_,_)=>{if(view.CanGoBack)view.GoBack();};
  forward.Click+=(_,_)=>{if(view.CanGoForward)view.GoForward();};
  reload.Click+=(_,_)=>view.Reload();
  strip.Items.Add(back);strip.Items.Add(forward);strip.Items.Add(reload);strip.Items.Add(new ToolStripSeparator());
  for(int i=0;i<names.Length;i++){
   int index=i;
   var btn=new ToolStripButton(names[i]);
   btn.Click+=(_,_)=>SelectSite(index);
   strip.Items.Add(btn);
  }
  strip.Items.Add(new ToolStripSeparator());
  strip.Items.Add(new ToolStripLabel("Downloads save automatically by website · Unity packages require Unity import"));
  strip.Items.Add(new ToolStripSeparator());
  var folder=new ToolStripButton("Asset folder");
  folder.Click+=(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe",$"\"{assetsRoot}\""){UseShellExecute=true});
  strip.Items.Add(folder);
  strip.Items.Add(new ToolStripSeparator());
  var close=new ToolStripButton("✕  Close Assets");
  close.ForeColor=Color.DarkRed;
  close.Click+=(_,_)=>Close();
  strip.Items.Add(close);
  strip.Items.Add(status);
  Controls.Add(view);Controls.Add(strip);
  strip.Dock=DockStyle.Top;
  strip.AutoSize=false;strip.Height=52;
  strip.Font=new Font("Segoe UI",11f,FontStyle.Regular);
  strip.BackColor=Color.FromArgb(235,241,248);
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
    SelectSite(0);
   }catch(Exception e){status.Text="Browser unavailable: "+e.Message;}
   // A normal resizable, closable browser window is used rather than a borderless
   // Win32 child which could cover Unity controls and conceal provider tabs.
   Activate();
  };
  FormClosed+=(_,_)=>layoutTimer.Stop();
 }
 void SelectSite(int i){
  provider=names[i];
  Text="WorldBuilder Assets - "+provider;
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
  if(!IsWindow(unity))return;
  var h=Handle;
  int style=GetWindowLong(h,GWL_STYLE);
  SetWindowLong(h,GWL_STYLE,(style|WS_CHILD)&~WS_POPUP);
  SetParent(h,unity);
  Position();
 }
 void Position(){
  if(!IsWindow(unity)){Close();return;}
  if(!GetClientRect(unity,out var rect))return;
  int width=Math.Max(480,rect.Right-rect.Left-218);
  int height=Math.Max(280,rect.Bottom-rect.Top-128);
  MoveWindow(Handle,207,68,width,height,true);
 }
}
