using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WorldBuilder {
 // Small runtime OBJ reader for static meshes. Supports v/vt/vn/f, triangulated polygons,
 // negative indices and one diffuse PNG/JPG from a neighboring MTL file.
 public static class ImportedObj {
  struct FaceVertex {public int v,uv,n;}
  static readonly CultureInfo CI=CultureInfo.InvariantCulture;
  static float Number(string s){return float.Parse(s,CI);}
  static int Index(string value,int count){
   if(string.IsNullOrEmpty(value))return -1;
   int n=int.Parse(value,CI);
   int idx=n>0?n-1:count+n;
   if(idx<0||idx>=count)throw new InvalidDataException("OBJ references an invalid vertex index");
   return idx;
  }
  public static GameObject Load(string path){
   var vertices=new List<Vector3>();
   var uvs=new List<Vector2>();
   var normals=new List<Vector3>();
   var outVertices=new List<Vector3>();
   var outUvs=new List<Vector2>();
   var outNormals=new List<Vector3>();
   var triangles=new List<int>();
   var map=new Dictionary<string,int>();
   string mtllib=null;
   foreach(string raw in File.ReadLines(path)){
    var line=raw.Trim();
    if(line.Length==0||line[0]=='#')continue;
    var terms=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
    if(terms.Length==0)continue;
    if(terms[0]=="v"&&terms.Length>=4)vertices.Add(new Vector3(-Number(terms[1]),Number(terms[2]),Number(terms[3])));
    else if(terms[0]=="vt"&&terms.Length>=3)uvs.Add(new Vector2(Number(terms[1]),Number(terms[2])));
    else if(terms[0]=="vn"&&terms.Length>=4)normals.Add(new Vector3(-Number(terms[1]),Number(terms[2]),Number(terms[3])));
    else if(terms[0]=="mtllib"&&terms.Length>=2)mtllib=terms[1];
    else if(terms[0]=="f"&&terms.Length>=4){
     var face=new List<int>();
     for(int i=1;i<terms.Length;i++){
      string key=terms[i];
      if(!map.TryGetValue(key,out int ix)){
       var bits=key.Split('/');
       int v=Index(bits[0],vertices.Count);
       ix=outVertices.Count;
       outVertices.Add(vertices[v]);
       outUvs.Add(bits.Length>1&&!string.IsNullOrEmpty(bits[1])?uvs[Index(bits[1],uvs.Count)]:Vector2.zero);
       outNormals.Add(bits.Length>2&&!string.IsNullOrEmpty(bits[2])?normals[Index(bits[2],normals.Count)]:Vector3.zero);
       map.Add(key,ix);
      }
      face.Add(ix);
     }
     for(int i=1;i<face.Count-1;i++){
      // Mirroring X changes handedness, so reverse winding.
      triangles.Add(face[0]);triangles.Add(face[i+1]);triangles.Add(face[i]);
     }
    }
   }
   if(triangles.Count==0)throw new InvalidDataException("OBJ has no mesh faces");
   var mesh=new Mesh();
   mesh.name=Path.GetFileNameWithoutExtension(path);
   if(outVertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
   mesh.SetVertices(outVertices);
   mesh.SetUVs(0,outUvs);
   mesh.SetTriangles(triangles,0);
   if(normals.Count>0)mesh.SetNormals(outNormals);else mesh.RecalculateNormals();
   mesh.RecalculateBounds();
   var obj=new GameObject(mesh.name);
   obj.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=obj.AddComponent<MeshRenderer>();
   var shader=Shader.Find("Standard");
   if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
   var mat=new Material(shader);
   mat.color=Color.white;
   if(!string.IsNullOrEmpty(mtllib)){
    string mtl=Path.Combine(Path.GetDirectoryName(path),Path.GetFileName(mtllib));
    if(File.Exists(mtl)){
     foreach(string raw in File.ReadLines(mtl)){
      var line=raw.Trim();
      if(line.StartsWith("Kd ",StringComparison.Ordinal)){
       var t=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
       if(t.Length>=4)mat.color=new Color(Number(t[1]),Number(t[2]),Number(t[3]));
      }else if(line.StartsWith("map_Kd ",StringComparison.Ordinal)){
       string tex=Path.Combine(Path.GetDirectoryName(path),Path.GetFileName(line.Substring(7).Trim()));
       if(File.Exists(tex)){
        byte[] bytes=File.ReadAllBytes(tex);
        var texture=new Texture2D(2,2);
        if(texture.LoadImage(bytes))mat.mainTexture=texture;
       }
      }
     }
    }
   }
   renderer.sharedMaterial=mat;
   obj.AddComponent<MeshCollider>().sharedMesh=mesh;
   return obj;
  }
 }
}
