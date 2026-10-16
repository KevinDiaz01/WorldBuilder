#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WorldBuilder {
 // Build-time smoke tests run before packaging the Windows editor.
 public static class WorldBuilderRegressionChecks {
  static void Expect(bool condition,string description){
   if(!condition)throw new InvalidOperationException("WorldBuilder regression: "+description);
  }
  public static void Run(WorldBuilderRuntime runtime){
   if(runtime==null)throw new ArgumentNullException(nameof(runtime));
   ToolSwitching(runtime);
   MeshImports();
   Geometry();
   Debug.Log("WorldBuilder regression checks passed: tool switching, OBJ materials, polygon and terrain");
  }
  static void ToolSwitching(WorldBuilderRuntime runtime){
   Type type=typeof(WorldBuilderRuntime);
   MethodInfo select=type.GetMethod("SelectTool",BindingFlags.Instance|BindingFlags.NonPublic);
   FieldInfo width=type.GetField("openingWidth",BindingFlags.Instance|BindingFlags.NonPublic);
   if(select==null||width==null)throw new InvalidOperationException("Tool selection controls missing");
   select.Invoke(runtime,new object[]{WorldBuilderRuntime.Tool.Garage});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Garage,"Garage tool never became active");
   Expect(Mathf.Abs((float)width.GetValue(runtime)-3f)<.01f,"Garage preset width");
   select.Invoke(runtime,new object[]{WorldBuilderRuntime.Tool.Window});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Window,"Window remained stuck on garage");
   Expect(Mathf.Abs((float)width.GetValue(runtime)-1.25f)<.01f,"Window inherited garage width");
   select.Invoke(runtime,new object[]{WorldBuilderRuntime.Tool.Door});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Door,"Door remained stuck on window");
   Expect(Mathf.Abs((float)width.GetValue(runtime)-.95f)<.01f,"Door inherited previous dimensions");
   select.Invoke(runtime,new object[]{WorldBuilderRuntime.Tool.Select});
  }
  static void MeshImports(){
   string folder=Path.Combine(Application.temporaryCachePath,"WB-regression-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   GameObject obj=null;
   try{
    string objPath=Path.Combine(folder,"model.obj");
    File.WriteAllText(Path.Combine(folder,"model.mtl"),
     "newmtl Red\nKd 1 0 0\nnewmtl Blue\nKd 0 0 1\n");
    File.WriteAllText(objPath,
     "mtllib model.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\n"+
     "usemtl Red\nf 1 2 3\nusemtl Blue\nf 1 3 4\n");
    obj=ImportedObj.Load(objPath);
    var filter=obj.GetComponent<MeshFilter>();
    var renderer=obj.GetComponent<MeshRenderer>();
    Expect(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.subMeshCount==2,
     "OBJ material faces collapsed to one submesh");
    Expect(renderer!=null&&renderer.sharedMaterials.Length==2,
     "OBJ material count does not match submeshes");
    Expect(renderer.sharedMaterials[0].color.r>.8f&&renderer.sharedMaterials[1].color.b>.8f,
     "OBJ material colors not preserved");
   }finally{
    if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
    if(Directory.Exists(folder))Directory.Delete(folder,true);
   }
  }
  static void Geometry(){
   Mesh floor=null,hill=null;
   try{
    var points=new List<Vector3>{
     new Vector3(0,0,0),new Vector3(3,0,0),new Vector3(3,0,1),
     new Vector3(1,0,1),new Vector3(1,0,3),new Vector3(0,0,3)};
    floor=PolygonGeometry.Create(points,.15f);
    Expect(floor.vertexCount==12&&floor.triangles.Length>=24,
     "Concave polygon floor generation failed");
    hill=TerrainGeometry.Create(12,8f,6f,2f,4);
    Expect(hill.vertexCount==169&&hill.triangles.Length==12*12*6,
     "Terrain mesh dimensions failed");
   }finally{
    if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);
    if(hill!=null)UnityEngine.Object.DestroyImmediate(hill);
   }
  }
 }
}
#endif
