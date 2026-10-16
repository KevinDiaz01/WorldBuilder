using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder {
 // Builds concave simple polygons with ear clipping; rejects self-intersecting outlines upstream.
 public static class PolygonGeometry {
  static float Cross(Vector2 a,Vector2 b,Vector2 c){return (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);}
  static bool InTri(Vector2 p,Vector2 a,Vector2 b,Vector2 c){
   float c1=Cross(a,b,p),c2=Cross(b,c,p),c3=Cross(c,a,p);
   return c1>=-.00001f&&c2>=-.00001f&&c3>=-.00001f;
  }
  public static Mesh Create(IList<Vector3> corners,float thickness){
   if(corners==null||corners.Count<3)throw new ArgumentException("Polygon requires three corners");
   int n=corners.Count;
   var points=new Vector2[n];float area=0f;
   for(int i=0;i<n;i++){
    points[i]=new Vector2(corners[i].x,corners[i].z);
    int j=(i+1)%n;
    area+=corners[i].x*corners[j].z-corners[j].x*corners[i].z;
   }
   if(Mathf.Abs(area)<.0001f)throw new ArgumentException("Polygon has zero area");
   var ring=new List<int>();
   if(area>0)for(int i=0;i<n;i++)ring.Add(i);
   else for(int i=n-1;i>=0;i--)ring.Add(i);
   var top=new List<int>();int guard=0;
   while(ring.Count>3&&guard++<n*n){
    bool found=false;
    for(int k=0;k<ring.Count;k++){
     int a=ring[(k+ring.Count-1)%ring.Count],b=ring[k],c=ring[(k+1)%ring.Count];
     if(Cross(points[a],points[b],points[c])<=.00001f)continue;
     bool occupied=false;
     foreach(int p in ring)if(p!=a&&p!=b&&p!=c&&InTri(points[p],points[a],points[b],points[c])){occupied=true;break;}
     if(occupied)continue;
     top.Add(a);top.Add(b);top.Add(c);ring.RemoveAt(k);found=true;break;
    }
    if(!found)throw new ArgumentException("Polygon intersects itself or contains overlapping corners");
   }
   if(ring.Count!=3)throw new ArgumentException("Could not triangulate polygon");
   top.Add(ring[0]);top.Add(ring[1]);top.Add(ring[2]);
   var vertices=new Vector3[n*2];var uv=new Vector2[n*2];float y=corners[0].y;
   for(int i=0;i<n;i++){
    vertices[i]=new Vector3(corners[i].x,y+thickness*.5f,corners[i].z);
    vertices[i+n]=new Vector3(corners[i].x,y-thickness*.5f,corners[i].z);
    uv[i]=uv[i+n]=new Vector2(corners[i].x,corners[i].z);
   }
   var indices=new List<int>();
   for(int k=0;k<top.Count;k+=3){
    indices.Add(top[k]);indices.Add(top[k+2]);indices.Add(top[k+1]);
    indices.Add(top[k]+n);indices.Add(top[k+1]+n);indices.Add(top[k+2]+n);
   }
   for(int k=0;k<n;k++){
    int a=k,b=(k+1)%n;
    indices.Add(a);indices.Add(b);indices.Add(a+n);
    indices.Add(b);indices.Add(b+n);indices.Add(a+n);
   }
   Mesh mesh=new Mesh();mesh.name="WB Polygon";
   if(vertices.Length>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
   mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices.ToArray();
   mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
 }
}