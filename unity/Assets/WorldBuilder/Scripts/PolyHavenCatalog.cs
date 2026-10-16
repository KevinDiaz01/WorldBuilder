using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace WorldBuilder {
 // Native in-application catalog. No external browser or account required.
 // Downloads are stored under WorldBuilder Assets/Poly Haven/Downloads.
 public class PolyHavenCatalog : MonoBehaviour {
  public class Item {public string id,name,category,thumbnail;}
  public List<Item> Items {get;private set;}=new List<Item>();
  public string State {get;private set;}="Ready";
  public bool Busy {get;private set;}
  public float Progress {get;private set;}
  const string Base="https://api.polyhaven.com/";
  const string Agent="WorldBuilder-AssetHub/0.1 (Unity Windows)";
  void Start(){Refresh();}
  public void Refresh(){if(!Busy)StartCoroutine(Fetch());}
  IEnumerator Fetch(){
   Busy=true;State="Loading Poly Haven catalog...";Progress=0;
   using(var req=UnityWebRequest.Get(Base+"assets?type=models")){
    req.SetRequestHeader("User-Agent",Agent);
    yield return req.SendWebRequest();
    if(req.result!=UnityWebRequest.Result.Success){State="Catalog failed: "+req.error;Busy=false;yield break;}
    try{
     JObject json=JObject.Parse(req.downloadHandler.text);
     var items=new List<Item>();
     foreach(var prop in json.Properties()){
      var value=prop.Value;
      items.Add(new Item{id=prop.Name,name=(string)value["name"]??prop.Name,
       category=(string)value["category"]??"",thumbnail=(string)value["thumbnail_url"]??""});
     }
     items.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
     Items=items;State=items.Count+" models available from Poly Haven";
    }catch(Exception e){State="Catalog data error: "+e.Message;}
   }
   Busy=false;
  }
  static bool FindDownload(JToken tree, out string url, out JToken node){
   url=null;node=null;
   if(tree==null)return false;
   if(tree.Type==JTokenType.Object){
    var direct=tree["url"];
    if(direct!=null && direct.Type==JTokenType.String && ((string)direct).StartsWith("https://",StringComparison.OrdinalIgnoreCase)){
     url=(string)direct;node=tree;return true;
    }
    foreach(var prop in ((JObject)tree).Properties())
     if(FindDownload(prop.Value,out url,out node))return true;
   }else if(tree.Type==JTokenType.Array)
    foreach(var child in (JArray)tree)if(FindDownload(child,out url,out node))return true;
   return false;
  }
  static JToken PreferredFile(JObject data){
   // Prefer a 1K/2K glTF download when available; this is a workflow
   // download, not yet a supported WorldBuilder model import.
   JToken gltf=data["gltf"]??data["glTF"]??data["glb"];
   if(gltf==null)return null;
   return gltf["2k"]??gltf["1k"]??gltf["4k"]??gltf;
  }
  public void Download(Item item){if(!Busy&&item!=null)StartCoroutine(FetchAndDownload(item));}
  IEnumerator FetchAndDownload(Item item){
   Busy=true;Progress=0;State="Checking files for "+item.name+"...";
   string url=null;JToken fileNode=null;
   using(var req=UnityWebRequest.Get(Base+"files/"+UnityWebRequest.EscapeURL(item.id))){
    req.SetRequestHeader("User-Agent",Agent);
    yield return req.SendWebRequest();
    if(req.result!=UnityWebRequest.Result.Success){State="Download information failed: "+req.error;Busy=false;yield break;}
    try{
     JObject files=JObject.Parse(req.downloadHandler.text);
     JToken preferred=PreferredFile(files);
     if(preferred!=null)FindDownload(preferred,out url,out fileNode);
     if(url==null)State="No glTF download available for this model.";
    }catch(Exception e){State="Unsupported asset metadata: "+e.Message;}
   }
   if(url==null){Busy=false;yield break;}
   string folder=AssetHub.Downloads("Poly Haven");
   string clean=string.Concat(item.id.Split(Path.GetInvalidFileNameChars()));
   string extension=Path.GetExtension(new Uri(url).AbsolutePath);
   if(string.IsNullOrEmpty(extension)||extension.Length>8)extension=".bin";
   string filename=Path.Combine(folder,clean+extension);
   string temporary=filename+".download";
   State="Downloading "+item.name+"...";
   try{if(File.Exists(temporary))File.Delete(temporary);}catch(Exception e){State=e.Message;Busy=false;yield break;}
   using(var request=UnityWebRequest.Get(url)){
    request.SetRequestHeader("User-Agent",Agent);
    request.downloadHandler=new DownloadHandlerFile(temporary);
    var job=request.SendWebRequest();
    while(!job.isDone){Progress=request.downloadProgress;yield return null;}
    if(request.result!=UnityWebRequest.Result.Success){
     State="Download failed: "+request.error;
     if(File.Exists(temporary))File.Delete(temporary);
     Busy=false;yield break;
    }
   }
   try{
    if(File.Exists(filename))File.Delete(filename);
    File.Move(temporary,filename);
    State="Saved "+Path.GetFileName(filename)+" to Poly Haven Downloads. Format import comes next.";
    Progress=1;
    // AssetHub intentionally scans only OBJ/ZIP-with-OBJ until
    // model format support is added.
   }catch(Exception e){State="Save failed: "+e.Message;}
   Busy=false;
  }
 }
}
