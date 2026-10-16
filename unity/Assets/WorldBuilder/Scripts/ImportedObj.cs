using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WorldBuilder {
 // Static Wavefront OBJ importer: independent materials/submeshes, triangulated faces,
 // negative indices, diffuse colors and PNG/JPEG textures from associated MTLs.
 public static class ImportedObj {
  sealed class Surface {
   public string name;
   public Color color=Color.white;
   public string texture;
  }
  static readonly CultureInfo CI=CultureInfo.InvariantCulture;
  static float Number(string s){return float.Parse(s,CI);}
  static int Index(string value,int count){
   if(string.IsNullOrEmpty(value))return -1;
   int n=int.Parse(value,CI);
   int ix=n>0?n-1:count+n;
   if(ix<0||ix>=count)throw new InvalidDataException("OBJ contains invalid geometry indices");
   return ix;
  }
  static string SafeTextureFile(string folder,string filename){
   if(string.IsNullOrWhiteSpace(filename))return null;
   string leaf=Path.GetFileName(filename.Trim().Trim('"').Replace('\\','/'));
   if(string.IsNullOrEmpty(leaf))return null;
   string file=Path.Combine(folder,leaf);
   return File.Exists(file)?file:null;
  }
  static void ReadMaterials(string path,Dictionary<string,Surface> surfaces){
   if(!File.Exists(path))return;
   string baseDir=Path.GetDirectoryName(path);
   Surface current=null;
   foreach(string raw in File.ReadLines(path)){
    string line=raw.Trim();
    if(line.Length==0||line[0]=='#')continue;
    int split=line.IndexOf(' ');
    if(split<0)continue;
    string word=line.Substring(0,split),value=line.Substring(split+1).Trim();
    if(word=="newmtl"){
     current=new Surface{name=value};
     surfaces[value]=current;
    }else if(current!=null&&word=="Kd"){
     var t=value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
     if(t.Length>=3)current.color=new Color(Number(t[0]),Number(t[1]),Number(t[2]),1f);
    }else if(current!=null&&(word=="map_Kd"||word=="map_BaseColor")){
     // Blender/Sketchfab MTL exporters sometimes prefix map options.
     string source=value;
     string[] words=value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
     if(words.Length>0&&source.StartsWith("-",StringComparison.Ordinal))source=words[words.Length-1];
     current.texture=SafeTextureFile(baseDir,source);
    }
   }
  }
  static Shader MeshShader(){
   return WorldBuilderShaders.Lit;
  }
  public static GameObject Load(string path){
   if(!File.Exists(path))throw new FileNotFoundException("Imported OBJ file is missing",path);
   var vertices=new List<Vector3>();
   var uvs=new List<Vector2>();
   var normals=new List<Vector3>();
   var outputVertices=new List<Vector3>();
   var outputUvs=new List<Vector2>();
   var outputNormals=new List<Vector3>();
   var lookup=new Dictionary<string,int>();
   var facesByMaterial=new Dictionary<string,List<int>>(StringComparer.Ordinal);
   var materialOrder=new List<string>();
   var materialDefs=new Dictionary<string,Surface>(StringComparer.Ordinal);
   string currentMaterial="__default";
   bool allNormals=true;
   void EnsureMaterial(string name){
    if(facesByMaterial.ContainsKey(name))return;
    facesByMaterial.Add(name,new List<int>());
    materialOrder.Add(name);
   }
   EnsureMaterial(currentMaterial);
   string folder=Path.GetDirectoryName(path);
   foreach(string raw in File.ReadLines(path)){
    string line=raw.Trim();
    if(line.Length==0||line[0]=='#')continue;
    var words=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
    if(words.Length<1)continue;
    if(words[0]=="v"&&words.Length>=4)
     vertices.Add(new Vector3(-Number(words[1]),Number(words[2]),Number(words[3])));
    else if(words[0]=="vt"&&words.Length>=3)
     uvs.Add(new Vector2(Number(words[1]),Number(words[2])));
    else if(words[0]=="vn"&&words.Length>=4)
     normals.Add(new Vector3(-Number(words[1]),Number(words[2]),Number(words[3])));
    else if(words[0]=="mtllib"&&words.Length>=2){
     string mtl=Path.Combine(folder,Path.GetFileName(line.Substring(7).Trim().Replace('\\','/')));
     ReadMaterials(mtl,materialDefs);
    }else if(words[0]=="usemtl"){
     currentMaterial=line.Substring(6).Trim();
     if(string.IsNullOrEmpty(currentMaterial))currentMaterial="__default";
     EnsureMaterial(currentMaterial);
    }else if(words[0]=="f"&&words.Length>=4){
     var indices=new List<int>(words.Length-1);
     for(int i=1;i<words.Length;i++){
      string key=words[i];
      if(!lookup.TryGetValue(key,out int ix)){
       string[] bits=key.Split('/');
       int v=Index(bits[0],vertices.Count);
       ix=outputVertices.Count;
       outputVertices.Add(vertices[v]);
       outputUvs.Add(bits.Length>1&&bits[1].Length>0?uvs[Index(bits[1],uvs.Count)]:Vector2.zero);
       int ni=bits.Length>2&&bits[2].Length>0?Index(bits[2],normals.Count):-1;
       if(ni<0)allNormals=false;
       outputNormals.Add(ni>=0?normals[ni]:Vector3.zero);
       lookup.Add(key,ix);
       if(ix>1000000)throw new InvalidDataException("Model is too detailed; limit is 1 million mesh vertices");
      }
      indices.Add(ix);
     }
     var triangles=facesByMaterial[currentMaterial];
     for(int i=1;i<indices.Count-1;i++){
      triangles.Add(indices[0]);triangles.Add(indices[i+1]);triangles.Add(indices[i]);
     }
    }
   }
   int materialCount=0;
   foreach(string key in materialOrder)if(facesByMaterial[key].Count>0)materialCount++;
   if(materialCount==0)throw new InvalidDataException("OBJ contains no supported triangle faces");
   var mesh=new Mesh();
   mesh.name=Path.GetFileNameWithoutExtension(path);
   if(outputVertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
   mesh.SetVertices(outputVertices);mesh.SetUVs(0,outputUvs);
   if(allNormals&&normals.Count>0)mesh.SetNormals(outputNormals);
   mesh.subMeshCount=materialCount;
   var rendererMaterials=new Material[materialCount];
   var textures=new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
   int submesh=0;
   foreach(string key in materialOrder){
    var indices=facesByMaterial[key];
    if(indices.Count==0)continue;
    mesh.SetTriangles(indices,submesh);
    var material=new Material(MeshShader());
    if(materialDefs.TryGetValue(key,out Surface source)){
     material.color=source.color;
     if(!string.IsNullOrEmpty(source.texture)){
      if(!textures.TryGetValue(source.texture,out Texture2D texture)){
       texture=new Texture2D(2,2,TextureFormat.RGBA32,true);
       if(!texture.LoadImage(File.ReadAllBytes(source.texture))){
        UnityEngine.Object.Destroy(texture);texture=null;
       }else{texture.wrapMode=TextureWrapMode.Repeat;}
       textures[source.texture]=texture;
      }
      if(texture!=null)material.mainTexture=texture;
     }
    }
    rendererMaterials[submesh++]=material;
   }
   if(!allNormals||normals.Count==0)mesh.RecalculateNormals();
   mesh.RecalculateBounds();
   var obj=new GameObject(mesh.name);
   obj.AddComponent<MeshFilter>().sharedMesh=mesh;
   obj.AddComponent<MeshRenderer>().sharedMaterials=rendererMaterials;
   obj.AddComponent<MeshCollider>().sharedMesh=mesh;
   return obj;
  }
 }
}