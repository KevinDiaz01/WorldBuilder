using UnityEngine;

namespace WorldBuilder {
 public static class TerrainGeometry {
  public static Mesh Create(int segments,float width,float depth,float height,int seed){
   int n=Mathf.Clamp(segments,4,80),side=n+1;
   Vector3[] vertices=new Vector3[side*side];
   Vector2[] uv=new Vector2[vertices.Length];
   int[] indices=new int[n*n*6];
   for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){
    float tx=(float)x/n,tz=(float)z/n;
    float edge=Mathf.Min(Mathf.Min(tx,1f-tx),Mathf.Min(tz,1f-tz));
    float taper=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(edge*3.4f));
    float broad=Mathf.PerlinNoise(tx*2.1f+seed*.027f,tz*2.1f+seed*.043f);
    float detail=Mathf.PerlinNoise(tx*5.2f+seed*.061f,tz*5.2f+seed*.078f);
    float noise=Mathf.Clamp01((broad*.78f+detail*.22f-.19f)*1.15f);
    int k=z*side+x;
    vertices[k]=new Vector3((tx-.5f)*width,noise*height*taper,(tz-.5f)*depth);
    uv[k]=new Vector2(tx*width*.2f,tz*depth*.2f);
   }
   int t=0;
   for(int z=0;z<n;z++)for(int x=0;x<n;x++){
    int a=z*side+x,b=a+1,c=a+side,d=c+1;
    indices[t++]=a;indices[t++]=c;indices[t++]=b;
    indices[t++]=b;indices[t++]=c;indices[t++]=d;
   }
   Mesh mesh=new Mesh();mesh.name="WB Terrain";
   mesh.vertices=vertices;mesh.triangles=indices;mesh.uv=uv;
   mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
 }
}