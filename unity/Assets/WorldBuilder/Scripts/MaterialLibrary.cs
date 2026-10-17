using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WorldBuilder {
 // Auto-detects color/albedo maps in folders and texture-only ZIP downloads.
 // Keeps a persistent material library separate from imported 3D objects.
 public static class MaterialLibrary {
  public struct Entry { public string id,name,provider; }
  public static string Root {
   get { string dir=Path.Combine(Application.persistentDataPath,"SurfaceMaterials");Directory.CreateDirectory(dir);return dir; }
  }
  static readonly Dictionary<string,string> observed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> failures=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  static string Id(string input){
   using(var sha=SHA256.Create()){
    var hash=sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
    return BitConverter.ToString(hash,0,12).Replace("-","").ToLowerInvariant();
   }
  }
  static bool Image(string file){
   string ext=Path.GetExtension(file).ToLowerInvariant();
   return ext==".png"||ext==".jpg"||ext==".jpeg";
  }
  static int Rank(string path){
   string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
   if(name.Contains("normal")||name.Contains("rough")||name.Contains("metal")||name.Contains("height")||
      name.Contains("displace")||name.Contains("ao")||name.Contains("opacity")||name.Contains("alpha")||
      name.Contains("specular")||name.Contains("bump"))return -100;
   if(name.Contains("basecolor")||name.Contains("base_color")||name.Contains("albedo"))return 100;
   if(name.Contains("diffuse")||name.Contains("color")||name.Contains("colour"))return 90;
   if(name.Contains("col_")||name.EndsWith("_col"))return 80;
   if(name.Contains("preview")||name.Contains("thumbnail"))return -50;
   return 10;
  }
  static string Pick(string[] files){
   string best=null;int score=25;
   foreach(var file in files){
    if(!Image(file))continue;
    int rank=Rank(file);
    if(rank>score){best=file;score=rank;}
   }
   return best;
  }
  static string FindMap(string[] files,string keyword){
   foreach(var file in files){
    if(!Image(file))continue;
    if(Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Contains(keyword))return file;
   }
   return null;
  }
  static string NiceName(string path){
   string name=Path.GetFileNameWithoutExtension(path).Replace('_',' ').Replace('-',' ');
   string[] suffixes={" basecolor"," base color"," albedo"," diffuse"," color"," colour"," 1k"," 2k"," 4k"," 8k"};
   foreach(string suffix in suffixes){
    int ix=name.ToLowerInvariant().IndexOf(suffix,StringComparison.Ordinal);
    if(ix>0)name=name.Substring(0,ix);
   }
   return string.IsNullOrWhiteSpace(name)?"Untitled Material":name.Trim();
  }
  static void Store(string key,string name,string provider,string color,string normal,string roughness,string signature){
   string id=Id(key);
   string dir=Path.Combine(Root,id);
   string stamp=Path.Combine(dir,"signature.txt");
   if(File.Exists(stamp)&&File.ReadAllText(stamp)==signature)return;
   Directory.CreateDirectory(dir);
   string ext=Path.GetExtension(color).ToLowerInvariant();
   string output=Path.Combine(dir,"albedo"+ext);
   File.Copy(color,output,true);
   // Remove previous albedo with another extension so the catalog chooses one.
   foreach(string old in Directory.GetFiles(dir,"albedo.*"))
    if(!string.Equals(old,output,StringComparison.OrdinalIgnoreCase))File.Delete(old);
   if(normal!=null)File.Copy(normal,Path.Combine(dir,"normal"+Path.GetExtension(normal).ToLowerInvariant()),true);
   if(roughness!=null)File.Copy(roughness,Path.Combine(dir,"roughness"+Path.GetExtension(roughness).ToLowerInvariant()),true);
   File.WriteAllText(Path.Combine(dir,"name.txt"),name);
   File.WriteAllText(Path.Combine(dir,"provider.txt"),provider);
   File.WriteAllText(stamp,signature);
   Diagnostics.Add("INFO","Material available: "+name+" ("+provider+")");
  }
  static string Signature(IEnumerable<string> paths){
   var sb=new StringBuilder();
   foreach(var file in paths){
    FileInfo info=new FileInfo(file);
    sb.Append(file).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append(';');
   }
   return Id(sb.ToString());
  }
  static void ScanFolder(string path,string provider){
   string[] images=Array.FindAll(Directory.GetFiles(path),file=>
    Image(file)&&AssetHub.RequestedCategory(file)!="Skyboxes"&&
    AssetHub.RequestedCategory(file)!="Materials");
   if(images.Length==0)return;
   Array.Sort(images,StringComparer.OrdinalIgnoreCase);
   // A model's texture directory is not an architectural surface library.
   // Only recognized albedo/color maps qualify for automatic folder materials.
   string albedo=Pick(images);
   if(albedo==null)return;
   string name=NiceName(Path.GetFileName(path));
   if(name==Path.GetFileName(path)&&name.Length<4)name=NiceName(albedo);
   Store("folder:"+path,name,provider,albedo,FindMap(images,"normal"),FindMap(images,"rough"),Signature(images));
  }
  static bool HasModelNearby(string folder,string providerRoot){
   string current=folder;
   string root=Path.GetFullPath(providerRoot).TrimEnd(Path.DirectorySeparatorChar);
   while(!string.IsNullOrEmpty(current)&&
     current.StartsWith(root,StringComparison.OrdinalIgnoreCase)){
    foreach(string file in Directory.GetFiles(current)){
     string ext=Path.GetExtension(file).ToLowerInvariant();
     if(ext==".obj"||ext==".glb"||ext==".gltf"||ext==".fbx"||
        ext==".blend"||ext==".mtl")return true;
    }
    if(string.Equals(current,root,StringComparison.OrdinalIgnoreCase))break;
    current=Path.GetDirectoryName(current);
   }
   return false;
  }
  static bool IsObjectTextureName(string name){
   name=(name??"").ToLowerInvariant();
   return name.Contains("statue")||name.Contains("bust")||name.Contains("character")||
    name.Contains("avatar")||name.Contains("sculpture")||name.Contains("figurine");
  }
  static bool IsModelArchive(string archive){
   try{
    using(var zip=ZipFile.OpenRead(archive)){
     foreach(var entry in zip.Entries){
      string ext=Path.GetExtension(entry.Name).ToLowerInvariant();
      if(ext==".obj"||ext==".glb"||ext==".gltf"||ext==".fbx"||ext==".blend")return true;
     }
    }
   }catch(IOException){return true;}
   return false;
  }
  static void ScanZip(string file,string provider){
   // Blender tree archives are 3D source packages, not paintable materials.
   if(file.EndsWith(".blend.zip",StringComparison.OrdinalIgnoreCase)||
      AssetHub.RequestedCategory(file)=="Skyboxes"||IsModelArchive(file))return;
   FileInfo info=new FileInfo(file);
   string signature=info.Length+"|"+info.LastWriteTimeUtc.Ticks;
   if(!observed.TryGetValue(file,out string before)||before!=signature){observed[file]=signature;return;}
   string id=Id("zip:"+file);
   string stamp=Path.Combine(Root,id,"signature.txt");
   if(File.Exists(stamp)&&File.ReadAllText(stamp)==signature)return;
   string staging=Path.Combine(Root,"staging-"+id);
   Directory.CreateDirectory(staging);
   try{
    var candidates=new List<string>();long total=0;int count=0;
    using(var zip=ZipFile.OpenRead(file)){
     foreach(var entry in zip.Entries){
      if(++count>3000)throw new InvalidDataException("Texture ZIP contains too many entries");
      total+=entry.Length;
      if(total>256L*1024*1024)throw new InvalidDataException("Texture ZIP exceeds 256 MB");
      if(!Image(entry.Name)||entry.Length<=0)continue;
      string ext=Path.GetExtension(entry.Name).ToLowerInvariant();
      string output=Path.Combine(staging,candidates.Count.ToString("D3")+"_"+Path.GetFileNameWithoutExtension(entry.Name)+ext);
      using(var input=entry.Open())using(var stream=File.Create(output))input.CopyTo(stream);
      candidates.Add(output);
     }
    }
    string[] files=candidates.ToArray();
    string albedo=Pick(files);
    if(albedo==null)return;
    Store("zip:"+file,NiceName(file),provider,albedo,FindMap(files,"normal"),FindMap(files,"rough"),signature);
   }finally{
    if(Directory.Exists(staging))Directory.Delete(staging,true);
   }
  }
  public static void Scan(){
   foreach(string provider in AssetHub.Providers){
    string root=AssetHub.Downloads(provider);
    try{
     var folders=new List<string>{root};
     folders.AddRange(Directory.GetDirectories(root,"*",SearchOption.AllDirectories));
     foreach(string dir in folders){
      string[] files=Directory.GetFiles(dir);
      // Each explicitly tagged standalone texture is its own material.
      // Do not combine all provider-root JPGs into one ambiguous material.
      foreach(string file in files){
       if(!Image(file)||AssetHub.RequestedCategory(file)!="Materials"||Rank(file)<0)continue;
       Store("single:"+file,NiceName(file),provider,file,null,null,Signature(new[]{file}));
      }
      bool hasModel=false;
      foreach(string file in files){
       string ext=Path.GetExtension(file).ToLowerInvariant();
       if(ext==".obj"||ext==".glb"||ext==".gltf"){hasModel=true;break;}
      }
      if(!hasModel&&!HasModelNearby(dir,root)&&!IsObjectTextureName(dir))ScanFolder(dir,provider);
     }
     foreach(string zip in Directory.GetFiles(root,"*.zip",SearchOption.AllDirectories))
      try{ScanZip(zip,provider);}catch(Exception ex){
       string key=zip+ex.Message;
       if(failures.Add(key))Diagnostics.Add("WARN","Texture package skipped: "+Path.GetFileName(zip)+" - "+ex.Message);
      }
    }catch(Exception ex){
     string key=provider+ex.Message;
     if(failures.Add(key))Diagnostics.Add("WARN","Material discovery: "+ex.Message);
    }
   }
  }
  public static List<Entry> Catalog(){
   var result=new List<Entry>();
   foreach(string dir in Directory.GetDirectories(Root)){
    if(Path.GetFileName(dir).StartsWith("staging-"))continue;
    string albedo=ColorFile(Path.GetFileName(dir));
    if(albedo==null)continue;
    string namePath=Path.Combine(dir,"name.txt"),providerPath=Path.Combine(dir,"provider.txt");
    string name=File.Exists(namePath)?File.ReadAllText(namePath):Path.GetFileName(dir);
    if(IsObjectTextureName(name))continue;
    result.Add(new Entry{id=Path.GetFileName(dir),name=name,
     provider=File.Exists(providerPath)?File.ReadAllText(providerPath):"Other"});
   }
   result.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
   return result;
  }
  public static string ColorFile(string id){
   string dir=Path.Combine(Root,id);
   if(!Directory.Exists(dir))return null;
   foreach(string ext in new[]{".png",".jpg",".jpeg"}){
    string path=Path.Combine(dir,"albedo"+ext);
    if(File.Exists(path))return path;
   }
   return null;
  }
 }
}