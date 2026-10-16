using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldBuilder {
 // Simple rectangular excavation meshes for pools and small water bodies.
 // Works with the project's existing rectangle-based Water tool.
 public static class BasinGeometry {
  static void AddCut(List<float> cuts,float value,float bound){
   value=Mathf.Clamp(value,-bound,bound);
   foreach(float existing in cuts)if(Mathf.Abs(existing-value)<.001f)return;
   cuts.Add(value);
  }
  public static List<Rect> Footprints(List<Piece> pieces){
   var result=new List<Rect>();
   if(pieces==null)return result;
   foreach(Piece piece in pieces){
    if(piece.kind!="water")continue;
    float width=Mathf.Max(.01f,piece.scale.x),length=Mathf.Max(.01f,piece.scale.z);
    result.Add(new Rect(piece.position.x-width*.5f,piece.position.z-length*.5f,width,length));
   }
   return result;
  }
  public static Mesh CreateMeadow(float halfExtent,List<Rect> basins){
   float radius=Mathf.Max(5f,halfExtent);
   var xs=new List<float>{-radius,radius};
   var zs=new List<float>{-radius,radius};
   foreach(Rect r in basins){
    AddCut(xs,r.xMin,radius);AddCut(xs,r.xMax,radius);
    AddCut(zs,r.yMin,radius);AddCut(zs,r.yMax,radius);
   }
   xs.Sort();zs.Sort();
   var verts=new List<Vector3>();
   var uvs=new List<Vector2>();
   var triangles=new List<int>();
   for(int i=0;i<xs.Count-1;i++)for(int j=0;j<zs.Count-1;j++){
    float x0=xs[i],x1=xs[i+1],z0=zs[j],z1=zs[j+1];
    if(x1-x0<.0001f||z1-z0<.0001f)continue;
    Vector2 center=new Vector2((x0+x1)*.5f,(z0+z1)*.5f);
    bool hole=false;
    foreach(Rect rect in basins)if(rect.Contains(center)){hole=true;break;}
    if(hole)continue;
    int k=verts.Count;
    verts.Add(new Vector3(x0,0,z0));verts.Add(new Vector3(x1,0,z0));
    verts.Add(new Vector3(x1,0,z1));verts.Add(new Vector3(x0,0,z1));
    uvs.Add(new Vector2((x0+radius)/(2f*radius),(z0+radius)/(2f*radius)));
    uvs.Add(new Vector2((x1+radius)/(2f*radius),(z0+radius)/(2f*radius)));
    uvs.Add(new Vector2((x1+radius)/(2f*radius),(z1+radius)/(2f*radius)));
    uvs.Add(new Vector2((x0+radius)/(2f*radius),(z1+radius)/(2f*radius)));
    triangles.Add(k);triangles.Add(k+2);triangles.Add(k+1);
    triangles.Add(k);triangles.Add(k+3);triangles.Add(k+2);
   }
   var mesh=new Mesh{name="WorldBuilder Ground With Water Cutouts"};
   mesh.indexFormat=IndexFormat.UInt32;
   mesh.SetVertices(verts);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
   mesh.RecalculateNormals();mesh.RecalculateBounds();
   return mesh;
  }
  public static Mesh CreateInterior(){
   // Parent object uses the water piece's x/depth/z scale.
   // Five inward-facing quads: bottom, north/south/east/west banks.
   var vertices=new List<Vector3>();
   var triangles=new List<int>();
   Action<Vector3,Vector3,Vector3,Vector3> face=(a,b,c,d)=>{
    int index=vertices.Count;
    vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
    triangles.Add(index);triangles.Add(index+2);triangles.Add(index+1);
    triangles.Add(index);triangles.Add(index+3);triangles.Add(index+2);
   };
   const float x=.5f;
   float btm=-.5f,top=.5f;
   face(new Vector3(-x,btm,-x),new Vector3(x,btm,-x),
    new Vector3(x,btm,x),new Vector3(-x,btm,x));
   face(new Vector3(-x,top,-x),new Vector3(x,top,-x),
    new Vector3(x,btm,-x),new Vector3(-x,btm,-x));
   face(new Vector3(x,top,x),new Vector3(-x,top,x),
    new Vector3(-x,btm,x),new Vector3(x,btm,x));
   face(new Vector3(-x,top,x),new Vector3(-x,top,-x),
    new Vector3(-x,btm,-x),new Vector3(-x,btm,x));
   face(new Vector3(x,top,-x),new Vector3(x,top,x),
    new Vector3(x,btm,x),new Vector3(x,btm,-x));
   var mesh=new Mesh{name="WorldBuilder Water Basin"};
   mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
   mesh.RecalculateNormals();mesh.RecalculateBounds();
   return mesh;
  }
  public static void CarveTerrain(Mesh mesh,Piece terrain,List<Piece> pieces){
   if(mesh==null||terrain==null||pieces==null)return;
   var vertices=mesh.vertices;
   for(int i=0;i<vertices.Length;i++){
    float x=vertices[i].x+terrain.position.x;
    float z=vertices[i].z+terrain.position.z;
    foreach(Piece water in pieces){
     if(water.kind!="water")continue;
     float dx=water.scale.x*.5f-Mathf.Abs(x-water.position.x);
     float dz=water.scale.z*.5f-Mathf.Abs(z-water.position.z);
     if(dx<=0||dz<=0)continue;
     float edge=Mathf.Min(dx,dz);
     float falloff=Mathf.Min(.8f,Mathf.Min(water.scale.x,water.scale.z)*.25f);
     float t=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(edge/Mathf.Max(.05f,falloff)));
     float surfaceY=water.position.y+water.scale.y*.5f;
     float bottomY=surfaceY-water.scale.y+.06f;
     float targetLocal=bottomY-terrain.position.y;
     vertices[i].y=Mathf.Lerp(vertices[i].y,Mathf.Min(vertices[i].y,targetLocal),t);
    }
   }
   mesh.vertices=vertices;
   mesh.RecalculateNormals();mesh.RecalculateBounds();
  }
 }
}
