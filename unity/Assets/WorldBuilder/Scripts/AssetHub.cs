using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace WorldBuilder {
 // First-stage offline asset hub. A future embedded browser will direct its downloads here.
 public static class AssetHub {
  public static readonly string[] Providers={"Poly Haven","Kenney","ambientCG","Other"};
  public static string Root {
   get {
    string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"WorldBuilder Assets");
    Directory.CreateDirectory(root);
    return root;
   }
  }
  public static string Downloads(string provider){
   if(Array.IndexOf(Providers,provider)<0)throw new InvalidDataException("Unrecognized asset provider");
   string folder=Path.Combine(Root,provider);
   Directory.CreateDirectory(folder);
   return folder;
  }
  public static void Initialize(){
   foreach(var provider in Providers)Downloads(provider);
  }
  public static string Library {get {
   string path=Path.Combine(Application.persistentDataPath,"ImportedAssets");
   Directory.CreateDirectory(path);return path;
  }}
  public static string Name(string id){
   string file=Path.Combine(Library,id,"asset-name.txt");
   return File.Exists(file)?File.ReadAllText(file):id;
  }
  public static List<string> Catalog(){
   var found=new List<string>();
   foreach(var folder in Directory.GetDirectories(Library)){
    if(File.Exists(Path.Combine(folder,"model.obj")))found.Add(Path.GetFileName(folder));
   }
   found.Sort((a,b)=>string.Compare(Name(a),Name(b),StringComparison.OrdinalIgnoreCase));
   return found;
  }
  public static int Scan(out string message){
   Initialize();
   int count=0;
   message="";
   foreach(string provider in Providers){
    var folders=new List<string>{Downloads(provider)};
    string older=Path.Combine(Downloads(provider),"Downloads");
    if(Directory.Exists(older))folders.Add(older);
    foreach(string folder in folders)foreach(string source in Directory.GetFiles(folder)){
     string ext=Path.GetExtension(source).ToLowerInvariant();
     if(ext!=".zip"&&ext!=".obj"&&ext!=".gltf"&&ext!=".glb")continue;
     string marker=source+".worldbuilder-imported";
     if(File.Exists(marker)&&File.ReadAllText(marker)==new FileInfo(source).Length.ToString())continue;
     // Never import a download while its writer still holds it.
     try{
      using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None)){}
      string id=Import(source,provider);
      File.WriteAllText(marker,new FileInfo(source).Length.ToString());
      count++;
      message="Imported "+Name(id)+" from "+provider;
     }catch(IOException){ /* unfinished download: retry next scan */ }
     catch(Exception e){
      message="Asset needs attention: "+Path.GetFileName(source)+" - "+e.Message;
      Debug.LogWarning("Asset Hub: "+message);
      // Permanent bad packages remain available for correction; avoid tight retry loops.
     }
    }
   }
   return count;
  }
  public static string Import(string source,string provider){
   if(!File.Exists(source))throw new FileNotFoundException("Asset file not found",source);
   string ext=Path.GetExtension(source).ToLowerInvariant();
   if(ext!=".obj"&&ext!=".zip"&&ext!=".gltf"&&ext!=".glb")throw new InvalidDataException("Supported files: OBJ, GLB, glTF and ZIP archives");
   string id=Guid.NewGuid().ToString("N");
   string destination=Path.Combine(Library,id);
   Directory.CreateDirectory(destination);
   try{
    string temp=Path.Combine(destination,"extracted");
    Directory.CreateDirectory(temp);
    if(ext==".zip")ExtractSafe(source,temp);
    else{
     File.Copy(source,Path.Combine(temp,Path.GetFileName(source)),true);
     foreach(string file in Directory.GetFiles(Path.GetDirectoryName(source))){
      string extension=Path.GetExtension(file).ToLowerInvariant();
      if(extension==".mtl"||extension==".png"||extension==".jpg"||extension==".jpeg"||extension==".bin"||extension==".webp")
       File.Copy(file,Path.Combine(temp,Path.GetFileName(file)),true);
     }
    }
    string[] models=Directory.GetFiles(temp,"*.obj",SearchOption.AllDirectories);
    if(models.Length==0){
     string[] glb=Directory.GetFiles(temp,"*.glb",SearchOption.AllDirectories);
     string[] gltf=Directory.GetFiles(temp,"*.gltf",SearchOption.AllDirectories);
     string[] candidates=glb.Length>0?glb:gltf;
     if(candidates.Length==0)throw new InvalidDataException("Archive contains no OBJ, GLB or glTF model");
     Array.Sort(candidates,StringComparer.OrdinalIgnoreCase);
     string converted=Path.Combine(temp,"model-converted.obj");
     GltfToObj.ConvertModel(candidates[0],converted);
     models=new[]{converted};
    }
    // First OBJ is a conservative first version. Multi-model packaging follows later.
    Array.Sort(models,StringComparer.OrdinalIgnoreCase);
    string selected=models[0];
    File.Copy(selected,Path.Combine(destination,"model.obj"),true);
    // Copy the model's MTL and texture companions including nested texture directories.
    string[] files=Directory.GetFiles(temp,"*",SearchOption.AllDirectories);
    foreach(var file in files){
     string extension=Path.GetExtension(file).ToLowerInvariant();
     if(extension==".mtl"||extension==".png"||extension==".jpg"||extension==".jpeg"){
      string dest=Path.Combine(destination,Path.GetFileName(file));
      if(!File.Exists(dest))File.Copy(file,dest);
     }
    }
    File.WriteAllText(Path.Combine(destination,"asset-name.txt"),Path.GetFileNameWithoutExtension(source));
    File.WriteAllText(Path.Combine(destination,"asset-source.txt"),provider);
    GameObject check=ImportedObj.Load(Path.Combine(destination,"model.obj"));
    ThumbnailUtility.Save(check,Path.Combine(destination,"thumbnail.png"));
    UnityEngine.Object.Destroy(check);
    Directory.Delete(temp,true);
    return id;
   }catch{
    if(Directory.Exists(destination))Directory.Delete(destination,true);
    throw;
   }
  }
  static void ExtractSafe(string zip,string target){
   const long maxBytes=512L*1024*1024;
   long total=0;
   int count=0;
   string canonical=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   using(var archive=ZipFile.OpenRead(zip)){
    foreach(var entry in archive.Entries){
     if(++count>5000)throw new InvalidDataException("Archive has too many files");
     total+=entry.Length;
     if(total>maxBytes)throw new InvalidDataException("Archive too large (512 MB extraction limit)");
     if(string.IsNullOrEmpty(entry.Name))continue;
     string path=Path.GetFullPath(Path.Combine(target,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
     if(!path.StartsWith(canonical,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe archive path");
     Directory.CreateDirectory(Path.GetDirectoryName(path));
     entry.ExtractToFile(path,true);
    }
   }
  }
 }
}
