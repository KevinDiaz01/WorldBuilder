using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace WorldBuilder {
 // First-stage offline asset hub. A future embedded browser will direct its downloads here.
 public static class AssetHub {
  public static readonly string[] Providers={"Poly Haven","Sketchfab","ambientCG","Other"};
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
  public static readonly string[] Categories={"All","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor","Architecture","Other","Trees","Bedroom","Living Room","Dining Room","Office"};
  public static string Category(string id){
   string path=Path.Combine(Library,id,"asset-category.txt");
   if(File.Exists(path)){
    string category=File.ReadAllText(path).Trim();
    if(Array.IndexOf(Categories,category)>=0)return category;
   }
   return GuessCategory(Name(id));
  }
  // The browser writes this sidecar only after downloads complete.
  public static string RequestedCategory(string source){
   string marker=source+".worldbuilder-category";
   if(!File.Exists(marker))return null;
   try{return File.ReadAllText(marker).Trim();}catch(IOException){return null;}
  }
  public static string ModelCategory(string source,string fallbackName){
   string category=RequestedCategory(source);
   if(category=="Tree"||category=="Trees")return "Trees";
   if(category=="Terrain"||category=="Water")return "Outdoor";
   if(category!=null&&Array.IndexOf(Categories,category)>=0&&category!="All")
    return category;
   return GuessCategory(fallbackName);
  }
  public static void SetCategory(string id,string category){
   if(Array.IndexOf(Categories,category)<0||category=="All")
    throw new InvalidDataException("Unknown model category");
   string directory=Path.Combine(Library,id);
   if(!File.Exists(Path.Combine(directory,"model.obj")))
    throw new FileNotFoundException("Cannot categorize a model not in My Assets");
   File.WriteAllText(Path.Combine(directory,"asset-category.txt"),category);
  }
  // Images shipped with one model remain associated with that model; they are
  // not mixed into the universal surface material palette.
  public static List<string> ModelTextures(string id){
   var found=new List<string>();
   if(!Guid.TryParseExact(id??"","N",out Guid ignored))return found;
   string folder=Path.Combine(Library,id);
   if(!File.Exists(Path.Combine(folder,"model.obj")))return found;
   foreach(string file in Directory.GetFiles(folder)){
    string extension=Path.GetExtension(file).ToLowerInvariant();
    if(extension==".png"||extension==".jpg"||extension==".jpeg"){
     if(!string.Equals(Path.GetFileName(file),"thumbnail.png",StringComparison.OrdinalIgnoreCase))
      found.Add(Path.GetFileName(file));
    }
   }
   found.Sort(StringComparer.OrdinalIgnoreCase);
   return found;
  }
  public static string ModelTexturePath(string id,string name){
   if(!Guid.TryParseExact(id??"","N",out Guid ignored)||string.IsNullOrWhiteSpace(name)||
    name!=Path.GetFileName(name))return null;
   string folder=Path.Combine(Library,id);
   if(!File.Exists(Path.Combine(folder,"model.obj")))return null;
   string path=Path.Combine(folder,name);
   string ext=Path.GetExtension(path).ToLowerInvariant();
   return (ext==".png"||ext==".jpg"||ext==".jpeg")&&File.Exists(path)?path:null;
  }
  public static string Provider(string id){
   string path=Path.Combine(Library,id,"asset-source.txt");
   return File.Exists(path)?File.ReadAllText(path).Trim():"Other";
  }
  public static string GuessCategory(string filename){
   string t=(filename??"").Replace('_',' ').Replace('-',' ').ToLowerInvariant();
   if(HasAny(t,"bed ","bedroom","mattress","nightstand","wardrobe","dresser"))return "Bedroom";
   if(HasAny(t,"sofa","couch","armchair","living room","tv stand","television"))return "Living Room";
   if(HasAny(t,"dining","dinner table"))return "Dining Room";
   if(HasAny(t,"desk","office","computer chair"))return "Office";
   if(HasAny(t,"chair","table","cabinet","shelf","stool","bench","furniture"))return "Furniture";
   if(HasAny(t,"lamp","chandelier","light fixture","lantern","sconce"))return "Lighting";
   if(HasAny(t,"fridge","refrigerator","oven","stove","sink","kitchen","microwave","toaster"))return "Kitchen";
   if(HasAny(t,"bathtub","toilet","shower","bathroom","washbasin"))return "Bathroom";
   if(HasAny(t,"tree","sapling","oak","pine","fir","palm","cedar","spruce"))return "Trees";
   if(HasAny(t,"plant","flower","bush","grass","fern","succulent","foliage"))return "Plants";
   if(HasAny(t,"rock","boulder","patio","fence","garden","outdoor","street"))return "Outdoor";
   if(HasAny(t,"wall","floor","window","door","stair","roof","building","house","architecture"))return "Architecture";
   if(HasAny(t,"vase","painting","art","candle","rug","carpet","pillow","curtain","mirror","decor"))return "Decor";
   return "Other";
  }
  static bool HasAny(string text,params string[] terms){
   foreach(string term in terms)if(text.Contains(term))return true;
   return false;
  }
  public static List<string> Catalog(){
   var found=new List<string>();
   foreach(var folder in Directory.GetDirectories(Library)){
    if(File.Exists(Path.Combine(folder,"model.obj")))found.Add(Path.GetFileName(folder));
   }
   found.Sort((a,b)=>string.Compare(Name(a),Name(b),StringComparison.OrdinalIgnoreCase));
   return found;
  }
  // Automatic recursive discovery. Events are recorded for the F2 console.
  static readonly Dictionary<string,string> observed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> reportedUnsupported=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> reportedTextureFolders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  static bool TextureOnlyZip(string source){
   if(!string.Equals(Path.GetExtension(source),".zip",StringComparison.OrdinalIgnoreCase))return false;
   using(var zip=ZipFile.OpenRead(source)){
    bool textures=false;
    foreach(var entry in zip.Entries){
     string ext=Path.GetExtension(entry.Name).ToLowerInvariant();
     if(ext==".obj"||ext==".glb"||ext==".gltf")return false;
     if(ext==".png"||ext==".jpg"||ext==".jpeg"||ext==".tga"||ext==".exr")textures=true;
    }
    return textures;
   }
  }
  public static int Scan(out string message){
   Initialize();
   int count=0;
   message="";
   foreach(string provider in Providers){
    string root=Downloads(provider);
    string[] files;
    try{files=Directory.GetFiles(root,"*",SearchOption.AllDirectories);}
    catch(Exception ex){Diagnostics.Add("ERROR","Cannot scan "+provider+": "+ex.Message);continue;}
    Array.Sort(files,StringComparer.OrdinalIgnoreCase);
    // Texture-only downloads are materials, not placeable 3D assets.
    // Explain this once rather than silently leaving My Assets unchanged.
    var textureFolders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var modelFolders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(string path in files){
     string suffix=Path.GetExtension(path).ToLowerInvariant();
     string dir=Path.GetDirectoryName(path);
     if(suffix==".obj"||suffix==".glb"||suffix==".gltf"||suffix==".zip")modelFolders.Add(dir);
     if(suffix==".png"||suffix==".jpg"||suffix==".jpeg"||suffix==".tga"||suffix==".exr")textureFolders.Add(dir);
    }
    foreach(string folder in textureFolders)
     if(!modelFolders.Contains(folder)&&reportedTextureFolders.Add(folder))
      Diagnostics.Add("INFO","Texture files detected in "+provider+": "+Path.GetFileName(folder)+". These are materials, not placeable 3D models.");
    foreach(string source in files){
     string ext=Path.GetExtension(source).ToLowerInvariant();
     if(source.EndsWith(".worldbuilder-imported",StringComparison.OrdinalIgnoreCase)||ext==".tmp"||ext==".crdownload"||ext==".part")continue;
     bool model=ext==".obj"||ext==".gltf"||ext==".glb"||ext==".zip";
     if(RequestedCategory(source)=="Skyboxes"||RequestedCategory(source)=="Materials")
      continue; // Separate library scanners handle these packages.
     if(!model){
      if((ext==".fbx"||ext==".blend"||ext==".unitypackage"||ext==".7z"||ext==".rar")&&reportedUnsupported.Add(source))
       Diagnostics.Add("WARN",ext==".unitypackage"?"Unity Asset Store package requires import in Unity: "+Path.GetFileName(source)+". WorldBuilder cannot place .unitypackage directly.":"Unsupported 3D format: "+Path.GetFileName(source)+" ("+provider+"). Use OBJ, GLB, glTF or ZIP.");
      continue;
     }
     try{
      FileInfo info=new FileInfo(source);
      string fingerprint=info.Length+"|"+info.LastWriteTimeUtc.Ticks;
      string marker=source+".worldbuilder-imported";
      if(File.Exists(marker)&&File.ReadAllText(marker)==fingerprint){observed.Remove(source);continue;}
      if(File.Exists(marker)&&File.ReadAllText(marker)==info.Length.ToString()){observed.Remove(source);continue;}
      // Require the file to be unchanged for two scans before reading it.
      // This prevents reading downloads that have not finished copying.
      if(!observed.TryGetValue(source,out string previous)||previous!=fingerprint){
       observed[source]=fingerprint;continue;
      }
      using(var handle=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None)){}
      if(ext==".zip"&&source.EndsWith(".blend.zip",StringComparison.OrdinalIgnoreCase)){
       if(reportedUnsupported.Add(source))Diagnostics.Add("WARN",
        "Blender model archive cannot be imported directly: "+Path.GetFileName(source)+
        ". Download the glTF/GLB or OBJ version from the asset website.");
       observed.Remove(source);continue;
      }
      if(ext==".zip"&&TextureOnlyZip(source)){
       // MaterialLibrary handles these; do not flag them as failed 3D models.
       observed.Remove(source);continue;
      }
      string id=Import(source,provider);
      File.WriteAllText(marker,fingerprint);
      observed.Remove(source);
      count++;
      message="Imported "+Name(id)+" from "+provider;
      Diagnostics.Add("INFO",message);
     }catch(IOException){ /* File is still being downloaded or copied. */ }
     catch(Exception ex){
      string problem=source+"|"+ex.Message;
      if(reportedUnsupported.Add(problem)){
       message="Could not import "+Path.GetFileName(source)+": "+ex.Message;
       Diagnostics.Add("ERROR",message);
      }
     }
    }
   }
   return count;
  }
  public static string Import(string source,string provider,string requestedCategory=null){
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
     // Preserve the source folder hierarchy for external glTF buffers and textures.
     string sourceFolder=Path.GetDirectoryName(source);
     foreach(string file in Directory.GetFiles(sourceFolder,"*",SearchOption.AllDirectories)){
      if(string.Equals(file,source,StringComparison.OrdinalIgnoreCase))continue;
      string extension=Path.GetExtension(file).ToLowerInvariant();
      if(extension==".mtl"||extension==".png"||extension==".jpg"||extension==".jpeg"||extension==".bin"||extension==".webp")
      {
       string relative=file.Substring(sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
       string target=Path.Combine(temp,relative);
       Directory.CreateDirectory(Path.GetDirectoryName(target));
       File.Copy(file,target,true);
      }
     }
    }
    string[] models=Directory.GetFiles(temp,"*.obj",SearchOption.AllDirectories);
    if(models.Length==0){
     string[] glb=Directory.GetFiles(temp,"*.glb",SearchOption.AllDirectories);
     string[] gltf=Directory.GetFiles(temp,"*.gltf",SearchOption.AllDirectories);
     var candidates=new List<string>();
     candidates.AddRange(glb);candidates.AddRange(gltf);
     if(candidates.Count==0)throw new InvalidDataException("Archive contains no OBJ, GLB or glTF model");
     candidates.Sort(StringComparer.OrdinalIgnoreCase);
     string converted=Path.Combine(temp,"model-converted.obj");
     var failures=new List<string>();
     bool convertedOk=false;
     // Providers sometimes bundle multiple resolutions or LODs in one ZIP.
     // An unsupported variant should not block all supported alternatives.
     foreach(string candidate in candidates){
      try{
       if(File.Exists(converted))File.Delete(converted);
       string staleMtl=Path.Combine(temp,"model.mtl");
       if(File.Exists(staleMtl))File.Delete(staleMtl);
       GltfToObj.ConvertModel(candidate,converted);
       convertedOk=true;break;
      }catch(Exception ex)when(ex is InvalidDataException||ex is NotSupportedException||
        ex is OverflowException||ex is EndOfStreamException){
       failures.Add(Path.GetFileName(candidate)+": "+ex.Message);
      }
     }
     if(!convertedOk)throw new InvalidDataException(
      "No compatible glTF found ("+candidates.Count+" attempted). "+
      string.Join(" | ",failures.ToArray())+
      ". Try a smaller resolution or the OBJ download.");
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
    string displayName=Path.GetFileNameWithoutExtension(source);
    File.WriteAllText(Path.Combine(destination,"asset-name.txt"),displayName);
    File.WriteAllText(Path.Combine(destination,"asset-source.txt"),provider);
    string category=string.IsNullOrEmpty(requestedCategory)?
     ModelCategory(source,displayName):requestedCategory;
    if(Array.IndexOf(Categories,category)<0||category=="All")category=GuessCategory(displayName);
    File.WriteAllText(Path.Combine(destination,"asset-category.txt"),category);
    GameObject check=null;
    try{
     check=ImportedObj.Load(Path.Combine(destination,"model.obj"));
     var renderers=check.GetComponentsInChildren<Renderer>();
     if(renderers.Length==0)throw new InvalidDataException("Model contains no visible mesh");
     Bounds bounds=renderers[0].bounds;
     for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
     float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
     if(float.IsNaN(largest)||float.IsInfinity(largest)||largest<.0001f)
      throw new InvalidDataException("Model contains unusable geometry. Try a different OBJ/GLB export.");
     File.WriteAllText(Path.Combine(destination,"model-size.txt"),
      bounds.size.x.ToString("F3")+" x "+bounds.size.y.ToString("F3")+" x "+bounds.size.z.ToString("F3")+" m");
     try{ThumbnailUtility.Save(check,Path.Combine(destination,"thumbnail.png"));}
     catch(Exception thumbError){
      Diagnostics.Add("WARN","Model imported but preview thumbnail failed: "+thumbError.Message);
     }
    }finally{if(check!=null)UnityEngine.Object.Destroy(check);}
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
