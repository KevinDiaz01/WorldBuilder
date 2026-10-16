using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WorldBuilderAssetBrowser;

// A separate, resizable window is deliberate: SetParent hid navigation/close
// controls on Windows 11 and non-100% DPI displays.
internal static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Length<2||!long.TryParse(args[0],out long hwnd)||hwnd==0)return;
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  Application.Run(new BrowserWindow(args[1],args.Length>2?args[2]:"Models"));
 }
}

internal static class AssetSearch {
 public static string Category(string raw){
  switch((raw??"").Trim().ToLowerInvariant()){
   case "materials": case "material": case "surface materials":return "Materials";
   case "sky": case "skybox": case "skyboxes": case "hdri": case "hdris":return "Skyboxes";
   case "trees": case "tree":return "Trees";
   case "plants": case "nature":return "Plants";
   case "landscape": case "terrain":return "Terrain";
   case "water":return "Water";
   case "furniture":return "Furniture";
   case "lighting":return "Lighting";
   case "decor":return "Decor";
   case "kitchen":return "Kitchen";
   case "bathroom":return "Bathroom";
   case "outdoor":return "Outdoor";
   case "architecture":return "Architecture";
   case "other":return "Other";
   default:return "Models";
  }
 }
 public static string Kind(string category){
  if(category=="Materials")return "material";
  if(category=="Skyboxes")return "skybox";
  return "model";
 }
 public static readonly string[] Providers={"Poly Haven","Sketchfab","ambientCG"};
 public static string Url(string category,int provider){
  string term=category=="Models"?"":category=="Terrain"?"ground":category.ToLowerInvariant();
  string q=Uri.EscapeDataString(term);
  if(category=="Skyboxes")return provider==0?"https://polyhaven.com/hdris":null;
  if(category=="Materials")return provider==0?"https://polyhaven.com/textures":
    provider==2?"https://ambientcg.com/list?type=Material":null;
  if(provider==0){
   if(category=="Trees")return "https://polyhaven.com/models/nature/trees";
   if(category=="Plants")return "https://polyhaven.com/models/nature/plants";
   if(category=="Furniture")return "https://polyhaven.com/models/furniture";
   if(category=="Lighting")return "https://polyhaven.com/models/lighting";
   if(category=="Architecture")return "https://polyhaven.com/models/architecture";
   return term==""?"https://polyhaven.com/models":"https://polyhaven.com/models?q="+q;
  }
  if(provider==1)return "https://sketchfab.com/search?features=downloadable&type=models"+(q==""?"":"&q="+q);
  return "https://ambientcg.com/list?type=3DModel"+(q==""?"":"&search="+q);
 }
 public static int Preferred(string category){return category=="Materials"?2:0;}
}

internal sealed class BrowserWindow:Form {
 readonly string assetsRoot,category;
 readonly WebView2 view=new(){Dock=DockStyle.Fill};
 readonly ToolStrip bar=new(){Dock=DockStyle.Top,AutoSize=false,Height=53,
  GripStyle=ToolStripGripStyle.Hidden,RenderMode=ToolStripRenderMode.Professional,
  BackColor=Color.FromArgb(242,245,253)};
 readonly Label help=new(){Dock=DockStyle.Top,Height=34,TextAlign=ContentAlignment.MiddleLeft,
  BackColor=Color.FromArgb(230,236,250),ForeColor=Color.FromArgb(38,56,91),
  Padding=new Padding(12,0,0,0)};
 readonly ToolStripLabel status=new("Ready"){ForeColor=Color.FromArgb(67,78,104)};
 readonly List<ToolStripButton> tabs=new();
 string provider="Poly Haven";
 public BrowserWindow(string root,string requestedCategory){
  assetsRoot=root;category=AssetSearch.Category(requestedCategory);
  Text="WorldBuilder | Find "+category;
  MinimumSize=new Size(850,560);Size=new Size(1280,770);
  StartPosition=FormStartPosition.CenterScreen;
  FormBorderStyle=FormBorderStyle.Sizable;ShowInTaskbar=true;
  KeyPreview=true;
  KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}};
  BackColor=Color.FromArgb(246,248,253);
  var close=new ToolStripButton("✕ Close"){Alignment=ToolStripItemAlignment.Right,ToolTipText="Close asset browser (Esc)"};
  close.Click+=(_,_)=>Close();
  var downloads=new ToolStripButton("Downloads"){Alignment=ToolStripItemAlignment.Right,ToolTipText="Open WorldBuilder Assets folder"};
  downloads.Click+=(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe",
    "\""+assetsRoot+"\""){UseShellExecute=true});
  bar.Items.Add(new ToolStripButton("←"){ToolTipText="Back"});
  bar.Items.Add(new ToolStripButton("→"){ToolTipText="Forward"});
  bar.Items.Add(new ToolStripButton("↻"){ToolTipText="Reload"});
  bar.Items[0].Click+=(_,_)=>{if(view.CanGoBack)view.GoBack();};
  bar.Items[1].Click+=(_,_)=>{if(view.CanGoForward)view.GoForward();};
  bar.Items[2].Click+=(_,_)=>{if(view.CoreWebView2!=null)view.Reload();};
  bar.Items.Add(new ToolStripSeparator());
  for(int i=0;i<AssetSearch.Providers.Length;i++){
   int index=i;
   var tab=new ToolStripButton(AssetSearch.Providers[i]){
    Font=new Font("Segoe UI",10f),Padding=new Padding(9,5,9,5),
    Margin=new Padding(2,5,2,5),AutoSize=true};
   tab.Click+=(_,_)=>SelectProvider(index);
   tabs.Add(tab);bar.Items.Add(tab);
  }
  bar.Items.Add(new ToolStripSeparator());
  bar.Items.Add(status);
  bar.Items.Add(downloads);bar.Items.Add(close);
  Controls.Add(view);Controls.Add(help);Controls.Add(bar);
  bar.BringToFront();help.BringToFront();
  Load+=async (_,_)=>{
   try{
    Directory.CreateDirectory(assetsRoot);
    var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "WorldBuilder","AssetBrowserProfile");
    Directory.CreateDirectory(profile);
    var env=await CoreWebView2Environment.CreateAsync(null,profile);
    await view.EnsureCoreWebView2Async(env);
    view.CoreWebView2.Settings.AreDevToolsEnabled=false;
    view.CoreWebView2.DownloadStarting+=Download;
    view.CoreWebView2.NewWindowRequested+=(_,args)=>{
     args.Handled=true;
     if(!string.IsNullOrWhiteSpace(args.Uri))view.CoreWebView2.Navigate(args.Uri);
    };
    view.CoreWebView2.NavigationCompleted+=(_,args)=>{
     if(!args.IsSuccess)status.Text="Page error: "+args.WebErrorStatus;
    };
    SelectProvider(AssetSearch.Preferred(category));
   }catch(Exception ex){help.Text="Browser initialization failed: "+ex.Message;}
  };
 }
 void SelectProvider(int i){
  string url=AssetSearch.Url(category,i);
  if(url==null){
   help.Text=AssetSearch.Providers[i]+" does not list "+AssetSearch.Kind(category)+
    " assets here. Choose an available source tab.";
   return;
  }
  for(int j=0;j<tabs.Count;j++){
   tabs[j].Checked=j==i;
   tabs[j].BackColor=j==i?Color.FromArgb(202,217,255):Color.FromArgb(242,245,253);
  }
  provider=AssetSearch.Providers[i];
  Text="WorldBuilder | "+category+" | "+provider;
  string kind=AssetSearch.Kind(category);
  help.Text="Find "+category+" · "+provider+"  |  Download "+(
   kind=="skybox"?"HDR panoramas (.hdr) or equirectangular JPG/PNG":
   kind=="material"?"albedo/PBR texture packs (ZIP, PNG, JPG)":"compatible OBJ/GLB/glTF/ZIP models")+
   "  |  Esc closes";
  if(view.CoreWebView2!=null)view.CoreWebView2.Navigate(url);
  status.Text=provider;
 }
 void Download(object? sender,CoreWebView2DownloadStartingEventArgs e){
  try{
   string directory=Path.Combine(assetsRoot,provider);
   Directory.CreateDirectory(directory);
   string filename=Path.GetFileName(e.ResultFilePath);
   if(string.IsNullOrWhiteSpace(filename))filename="download.bin";
   foreach(char c in Path.GetInvalidFileNameChars())filename=filename.Replace(c,'_');
   string target=Path.Combine(directory,filename);
   if(File.Exists(target))target=Path.Combine(directory,Path.GetFileNameWithoutExtension(filename)+
    "-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+Path.GetExtension(filename));
   e.ResultFilePath=target;
   e.Handled=true;
   string requested=category;
   status.Text="Downloading "+Path.GetFileName(target);
   e.DownloadOperation.StateChanged+=(_,_)=>{
    if(IsDisposed)return;
    if(e.DownloadOperation.State==CoreWebView2DownloadState.Completed){
     try{
      // This sidecar is the contract consumed by the model/material/sky scanners.
      // Never mark incomplete or interrupted files as importable.
      File.WriteAllText(target+".worldbuilder-category",requested);
      BeginInvoke(new Action(()=>status.Text="Downloaded "+Path.GetFileName(target)+" as "+requested));
     }catch(Exception ex){BeginInvoke(new Action(()=>status.Text="Category save failed: "+ex.Message));}
    }else if(e.DownloadOperation.State==CoreWebView2DownloadState.Interrupted)
     BeginInvoke(new Action(()=>status.Text="Download interrupted: "+Path.GetFileName(target)));
   };
  }catch(Exception ex){status.Text="Download error: "+ex.Message;}
 }
}
