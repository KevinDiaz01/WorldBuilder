using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WorldBuilder {
 // Imported environment panoramas are not mesh objects or surface paints.
 // Compatible files: equirectangular Radiance .hdr, .jpg and .png.
 // EXR requires an additional decoder and is not silently accepted.
 public static class SkyboxLibrary {
  public struct Entry { public string id,name,provider; }
  static readonly Dictionary<string,string> observed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> failures=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public static string Root {
   get {
    string dir=Path.Combine(Application.persistentDataPath,"Skyboxes");
    Directory.CreateDirectory(dir);
    return dir;
   }
  }
  static bool Supported(string path){
   string extension=Path.GetExtension(path).ToLowerInvariant();
   return extension==".hdr"||extension==".jpg"||extension==".jpeg"||extension==".png";
  }
  public static string ImagePath(string id){
   if(!Guid.TryParseExact(id??"","N",out Guid ignored))return null;
   string folder=Path.Combine(Root,id);
   if(!Directory.Exists(folder))return null;
   foreach(string extension in new[]{".hdr",".jpg",".jpeg",".png"}){
    string path=Path.Combine(folder,"image"+extension);
    if(File.Exists(path))return path;
   }
   return null;
  }
  public static List<Entry> Catalog(){
   var list=new List<Entry>();
   foreach(string folder in Directory.GetDirectories(Root)){
    string id=Path.GetFileName(folder);
    if(ImagePath(id)==null)continue;
    string name=Path.Combine(folder,"name.txt");
    string provider=Path.Combine(folder,"provider.txt");
    list.Add(new Entry{id=id,name=File.Exists(name)?File.ReadAllText(name):id,
     provider=File.Exists(provider)?File.ReadAllText(provider):"Other"});
   }
   list.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
   return list;
  }
  public static string Import(string source,string provider){
   if(!File.Exists(source)||!Supported(source))
    throw new InvalidDataException("Choose a Radiance HDR, JPG or PNG panorama");
   if(new FileInfo(source).Length>128L*1024*1024)
    throw new InvalidDataException("Sky panorama exceeds 128 MB. Download a smaller resolution.");
   string id=Guid.NewGuid().ToString("N");
   string folder=Path.Combine(Root,id);
   Directory.CreateDirectory(folder);
   try{
    string target=Path.Combine(folder,"image"+Path.GetExtension(source).ToLowerInvariant());
    File.Copy(source,target);
    // Reject malformed HDRs and images before adding them to the library.
    var preview=ReadTexture(target);
    if(preview==null)throw new InvalidDataException("Sky panorama could not be decoded");
    UnityEngine.Object.Destroy(preview);
    File.WriteAllText(Path.Combine(folder,"name.txt"),Path.GetFileNameWithoutExtension(source).Replace('_',' '));
    File.WriteAllText(Path.Combine(folder,"provider.txt"),provider);
    return id;
   }catch{
    if(Directory.Exists(folder))Directory.Delete(folder,true);
    throw;
   }
  }
  public static void Scan(){
   foreach(string provider in AssetHub.Providers){
    try{
     foreach(string file in Directory.GetFiles(AssetHub.Downloads(provider),"*",SearchOption.AllDirectories)){
      if(AssetHub.RequestedCategory(file)!="Skyboxes")continue;
      string ext=Path.GetExtension(file).ToLowerInvariant();
      if(!Supported(file)&&ext!=".zip"){
       if(failures.Add(file))Diagnostics.Add("WARN",
        "Unsupported HDRI "+Path.GetFileName(file)+". Download a .hdr, .jpg or .png panorama.");
       continue;
      }
      try{
       var info=new FileInfo(file);
       string fingerprint=info.Length+"|"+info.LastWriteTimeUtc.Ticks;
       string stamp=file+".worldbuilder-skyimported";
       if(File.Exists(stamp)&&File.ReadAllText(stamp)==fingerprint)continue;
       if(!observed.TryGetValue(file,out string before)||before!=fingerprint){
        observed[file]=fingerprint;continue;
       }
       using(var hold=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){}
       string source=file;
       string temp=null;
       try{
        if(ext==".zip"){
         temp=Path.Combine(Application.temporaryCachePath,"WB-sky-"+Guid.NewGuid().ToString("N"));
         Directory.CreateDirectory(temp);
         using(var archive=ZipFile.OpenRead(file)){
          ZipArchiveEntry chosen=null;
          foreach(var entry in archive.Entries){
           if(entry.Length>128L*1024*1024)continue;
           if(!Supported(entry.Name))continue;
           if(chosen==null||Path.GetExtension(entry.Name).Equals(".hdr",StringComparison.OrdinalIgnoreCase))
            chosen=entry;
           if(chosen==entry&&Path.GetExtension(entry.Name).Equals(".hdr",StringComparison.OrdinalIgnoreCase))break;
          }
          if(chosen==null)throw new InvalidDataException("Skybox ZIP has no supported HDR, JPG or PNG panorama");
          source=Path.Combine(temp,"panorama"+Path.GetExtension(chosen.Name).ToLowerInvariant());
          chosen.ExtractToFile(source);
         }
        }
        string id=Import(source,provider);
        if(temp!=null){
         File.WriteAllText(Path.Combine(Root,id,"name.txt"),Path.GetFileNameWithoutExtension(file));
        }
        File.WriteAllText(stamp,fingerprint);
        Diagnostics.Add("INFO","Skybox imported: "+Path.GetFileName(file)+" ("+provider+")");
       }finally{if(temp!=null&&Directory.Exists(temp))Directory.Delete(temp,true);}
       observed.Remove(file);
      }catch(IOException){} // Download not yet complete or temporarily locked.
      catch(Exception ex){
       if(failures.Add(file+"|"+ex.Message))
        Diagnostics.Add("WARN","Skybox import failed for "+Path.GetFileName(file)+": "+ex.Message);
      }
     }
    }catch(Exception ex){
     if(failures.Add(provider+"|"+ex.Message))
      Diagnostics.Add("WARN","Skybox discovery failed: "+ex.Message);
    }
   }
  }
  public static Texture2D LoadTexture(string id){
   string file=ImagePath(id);
   if(file==null)throw new FileNotFoundException("The selected skybox is missing");
   return ReadTexture(file);
  }
  static Texture2D ReadTexture(string path){
   if(Path.GetExtension(path).Equals(".hdr",StringComparison.OrdinalIgnoreCase))
    return ReadRadiance(path);
   var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
   if(!image.LoadImage(File.ReadAllBytes(path))){
    UnityEngine.Object.Destroy(image);
    throw new InvalidDataException("Invalid equirectangular JPG/PNG image");
   }
   image.wrapMode=TextureWrapMode.Repeat;
   return image;
  }
  static string HeaderLine(Stream stream){
   var buffer=new List<byte>();
   int b;
   while((b=stream.ReadByte())>=0&&b!=10){
    if(buffer.Count>1024)throw new InvalidDataException("Invalid HDR header");
    if(b!=13)buffer.Add((byte)b);
   }
   if(b<0&&buffer.Count==0)throw new EndOfStreamException();
   return Encoding.ASCII.GetString(buffer.ToArray());
  }
  static Texture2D ReadRadiance(string path){
   using(var input=File.OpenRead(path)){
    string first=HeaderLine(input);
    if(!first.StartsWith("#?RADIANCE",StringComparison.Ordinal)&&
       !first.StartsWith("#?RGBE",StringComparison.Ordinal))
     throw new InvalidDataException("Not a Radiance HDR panorama");
    bool format=false;
    for(int i=0;i<100;i++){
     string line=HeaderLine(input);
     if(line.StartsWith("FORMAT=32-bit_rle_rgbe",StringComparison.Ordinal))format=true;
     if(line.Length==0)break;
    }
    if(!format)throw new InvalidDataException("Unsupported Radiance HDR pixel encoding");
    string[] dimensions=HeaderLine(input).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
    if(dimensions.Length!=4||dimensions[0]!="-Y"||dimensions[2]!="+X"||
       !int.TryParse(dimensions[1],out int h)||!int.TryParse(dimensions[3],out int w)||
       h<2||w<2||w>16384||h>8192||(long)w*h>16777216)
     throw new InvalidDataException("Unsupported HDR dimensions or orientation");
    var colors=new Color[w*h];
    byte[][] channels={new byte[w],new byte[w],new byte[w],new byte[w]};
    for(int y=0;y<h;y++){
     int a=input.ReadByte(),b=input.ReadByte(),c=input.ReadByte(),d=input.ReadByte();
     if(a!=2||b!=2||((c<<8)|d)!=w||w<8||w>32767)
      throw new InvalidDataException("HDR scanline must use standard modern Radiance RLE");
     for(int channel=0;channel<4;channel++){
      int x=0;
      while(x<w){
       int n=input.ReadByte();
       if(n<0||n==0)throw new EndOfStreamException("Truncated HDR scanline");
       if(n>128){
        int run=n-128,value=input.ReadByte();
        if(value<0||x+run>w)throw new InvalidDataException("Invalid HDR run");
        for(int k=0;k<run;k++)channels[channel][x++]=(byte)value;
       }else{
        if(x+n>w)throw new InvalidDataException("Invalid HDR literal run");
        for(int k=0;k<n;k++){
         int value=input.ReadByte();
         if(value<0)throw new EndOfStreamException("Truncated HDR pixels");
         channels[channel][x++]=(byte)value;
        }
       }
      }
     }
     for(int x=0;x<w;x++){
      int exponent=channels[3][x];
      float scale=exponent==0?0f:Mathf.Pow(2f,exponent-136f);
      colors[y*w+x]=new Color(channels[0][x]*scale,channels[1][x]*scale,
       channels[2][x]*scale,1f);
     }
    }
    var texture=new Texture2D(w,h,TextureFormat.RGBAHalf,false,true);
    texture.SetPixels(colors);
    texture.wrapMode=TextureWrapMode.Repeat;
    texture.Apply(false,true);
    return texture;
   }
  }
 }
}
