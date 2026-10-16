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
   RuntimeShaders();
   WorkspaceLayout(runtime);
   SpanGestureEligibility();
   CatalogToolConsistency(runtime);
   ImportedModelPaint();
   AssociatedMaterials();
   AssetCategories();
   SkyboxImports();
   ToolSwitching(runtime);
   MeshImports();
   GltfMaterialRoundTrip();
   Geometry();
   Debug.Log("WorldBuilder regression checks passed: bundled shaders, tool switching, OBJ/glTF materials, polygon and terrain");
  }
  static void RuntimeShaders(){
   Shader lit=Resources.Load<Shader>("WorldBuilderLit");
   Shader overlay=Resources.Load<Shader>("WorldBuilderOverlay");
   Shader sky=Resources.Load<Shader>("WorldBuilderSky");
   Shader panorama=Resources.Load<Shader>("WorldBuilderPanoramaSky");
   Expect(lit!=null&&overlay!=null&&sky!=null&&panorama!=null,
    "standalone runtime shaders must include HDRI panoramas");
   Material a=WorldBuilderShaders.NewLit(Color.white);
   Material b=WorldBuilderShaders.NewOverlay(new Color(.2f,.4f,.6f,.5f));
   Material c=new Material(sky);
   Material d=new Material(panorama);
   try{
    Expect(a.shader==lit&&b.shader==overlay&&c.shader==sky&&d.shader==panorama,
     "runtime shaders are not used");
   }finally{
    UnityEngine.Object.DestroyImmediate(a);
    UnityEngine.Object.DestroyImmediate(b);
    UnityEngine.Object.DestroyImmediate(c);
    UnityEngine.Object.DestroyImmediate(d);
   }
  }
  static void WorkspaceLayout(WorldBuilderRuntime runtime){
   Type t=typeof(WorldBuilderRuntime);
   FieldInfo catalog=t.GetField("showCatalog",BindingFlags.Instance|BindingFlags.NonPublic);
   FieldInfo modes=t.GetField("catalogOpenForMode",BindingFlags.Instance|BindingFlags.NonPublic);
   MethodInfo height=t.GetMethod("CatalogHeight",BindingFlags.Instance|BindingFlags.NonPublic);
   Expect(catalog!=null&&modes!=null&&height!=null,"workspace layout controls missing");
   Expect(!(bool)catalog.GetValue(runtime),"Build viewport should initially be unobstructed");
   bool[] state=(bool[])modes.GetValue(runtime);
   Expect(state.Length==3&&!state[0]&&!state[1]&&!state[2],
    "mode palettes must start hidden; left rail opens contextual collections");
   Expect(Mathf.Abs((float)height.Invoke(runtime,new object[]{900f})-245f)<.01f,
     "catalog height should be capped at 245 pixels");
  }
  static void SpanGestureEligibility(){
   foreach(var tool in new[]{WorldBuilderRuntime.Tool.Wall,WorldBuilderRuntime.Tool.Floor,
    WorldBuilderRuntime.Tool.Room,WorldBuilderRuntime.Tool.Roof,WorldBuilderRuntime.Tool.Stairs,
    WorldBuilderRuntime.Tool.Terrain,WorldBuilderRuntime.Tool.Water,WorldBuilderRuntime.Tool.Path})
    Expect(WorldBuilderRuntime.IsSpanTool(tool),"drag support lost for "+tool);
   foreach(var tool in new[]{WorldBuilderRuntime.Tool.Select,WorldBuilderRuntime.Tool.Imported,
    WorldBuilderRuntime.Tool.Window,WorldBuilderRuntime.Tool.Garage,WorldBuilderRuntime.Tool.Erase})
    Expect(!WorldBuilderRuntime.IsSpanTool(tool),"single-click tool captured drag: "+tool);
  }
  static void CatalogToolConsistency(WorldBuilderRuntime runtime){
   MethodInfo choose=typeof(WorldBuilderRuntime).GetMethod("ChooseCatalogCategory",
     BindingFlags.Instance|BindingFlags.NonPublic);
   Expect(choose!=null,"category selection handler missing");
   choose.Invoke(runtime,new object[]{8});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Paint,"Materials should activate Paint");
   choose.Invoke(runtime,new object[]{10});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Select,"Other must deactivate Paint");
   choose.Invoke(runtime,new object[]{0});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Select,"My Assets should be non-painting");
   choose.Invoke(runtime,new object[]{12});
   Expect(runtime.currentTool==WorldBuilderRuntime.Tool.Select,"Skyboxes must not activate Paint");
  }
  static void ImportedModelPaint(){
   MethodInfo apply=typeof(WorldBuilderRuntime).GetMethod("ApplyImportedFinish",
    BindingFlags.Instance|BindingFlags.NonPublic);
   Expect(apply!=null,"imported mesh paint integration missing");
   // The finish property is serialized per placed piece, not per source
   // asset, so the same imported model can have independent coatings.
   Piece a=new Piece("imported",Vector3.zero,Vector3.one,Quaternion.identity);
   Piece b=new Piece("imported",Vector3.one,Vector3.one,Quaternion.identity);
   a.finish="texture:example";
   Expect(b.finish!=a.finish,"painting one model must not repaint other instances");
   var saved=JsonUtility.ToJson(a);
   Expect(JsonUtility.FromJson<Piece>(saved).finish==a.finish,
    "imported finish lost after saving");
  }
  static void AssociatedMaterials(){
   // Filename validation is important: model textures may be user-downloaded.
   Expect(AssetHub.ModelTexturePath("not-an-id","../../../secret.png")==null,
    "model texture path must reject untrusted ids and path traversal");
   MethodInfo finish=typeof(WorldBuilderRuntime).GetMethod("SetImportedFinish",
    BindingFlags.Instance|BindingFlags.NonPublic);
   Expect(finish!=null,"selected model must expose original and packaged finishes");
   var piece=new Piece("imported",Vector3.zero,Vector3.one,Quaternion.identity);
   piece.assetId=Guid.NewGuid().ToString("N");
   piece.finish="assettexture:albedo.png";
   Expect(JsonUtility.FromJson<Piece>(JsonUtility.ToJson(piece)).finish==piece.finish,
    "model-specific texture selection is not saved");
  }
  static void AssetCategories(){
   string folder=Path.Combine(Application.temporaryCachePath,"WB-categories-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   string source=Path.Combine(folder,"tree.obj");
   try{
    File.WriteAllText(source,"# empty placeholder for category test");
    File.WriteAllText(source+".worldbuilder-category","Trees");
    Expect(AssetHub.RequestedCategory(source)=="Trees","download intent lost");
    Expect(AssetHub.ModelCategory(source,"decorated crate")=="Trees","tree was guessed as Other");
    File.WriteAllText(source+".worldbuilder-category","Terrain");
    Expect(AssetHub.ModelCategory(source,"rock")=="Outdoor","terrain browsing category mapping");
    File.Delete(source+".worldbuilder-category");
    Expect(AssetHub.ModelCategory(source,"Oak Tree")=="Trees","tree fallback classification");
   }finally{Directory.Delete(folder,true);}
  }
  static void SkyboxImports(){
   // Minimal two-scanline Radiance RLE file, no network/asset store dependency.
   string folder=Path.Combine(Application.temporaryCachePath,"WB-skytest-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   string path=Path.Combine(folder,"valid.hdr");
   Texture2D texture=null;
   try{
    using(var stream=File.Create(path)){
     byte[] header=System.Text.Encoding.ASCII.GetBytes(
      "#?RADIANCE\\nFORMAT=32-bit_rle_rgbe\\n\\n-Y 2 +X 8\\n".Replace("\\n","\n"));
     stream.Write(header,0,header.Length);
     for(int y=0;y<2;y++){
      byte[] scanline={2,2,0,8,136,128,136,64,136,32,136,128};
      stream.Write(scanline,0,scanline.Length);
     }
    }
    MethodInfo read=typeof(SkyboxLibrary).GetMethod("ReadTexture",
     BindingFlags.Static|BindingFlags.NonPublic);
    Expect(read!=null,"sky decoder missing");
    texture=(Texture2D)read.Invoke(null,new object[]{path});
    Expect(texture!=null&&texture.width==8&&texture.height==2,"Radiance panorama decoder failed");
    var serialized=JsonUtility.ToJson(new Project{skyboxId="test-sky"});
    Expect(JsonUtility.FromJson<Project>(serialized).skyboxId=="test-sky","saved project lost sky selection");
   }finally{
    if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
    if(Directory.Exists(folder))Directory.Delete(folder,true);
   }
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
  static void GltfMaterialRoundTrip(){
   string folder=Path.Combine(Application.temporaryCachePath,"WB-gltf-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   GameObject converted=null;
   try{
    byte[] buffer=new byte[54];
    float[] vertices={0f,0f,0f,1f,0f,0f,0f,1f,0f,0f,0f,1f};
    for(int i=0;i<vertices.Length;i++)
     Buffer.BlockCopy(BitConverter.GetBytes(vertices[i]),0,buffer,i*4,4);
    for(int i=0;i<3;i++)
     Buffer.BlockCopy(BitConverter.GetBytes((ushort)i),0,buffer,48+i*2,2);
    string encoded=Convert.ToBase64String(buffer);
    string input=Path.Combine(folder,"two-materials.gltf");
    string json="{"+
     "\"buffers\":[{\"byteLength\":54,\"uri\":\"data:application/octet-stream;base64,"+encoded+"\"}],"+
     "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":48,\"byteLength\":6}],"+
     "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}],"+
     "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1,\"material\":0},{\"attributes\":{\"POSITION\":0},\"indices\":1,\"material\":1}]}],"+
     "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[1,0,0,1]}},{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0,0,1,1]}}],"+
     "\"nodes\":[{\"mesh\":0}],\"scenes\":[{\"nodes\":[0]}],\"scene\":0}";
    File.WriteAllText(input,json);
    string output=Path.Combine(folder,"model.obj");
    GltfToObj.ConvertModel(input,output);
    converted=ImportedObj.Load(output);
    var renderer=converted.GetComponent<MeshRenderer>();
    Expect(renderer!=null&&renderer.sharedMaterials.Length==2,
     "glTF conversion lost individual material submeshes");
   }finally{
    if(converted!=null)UnityEngine.Object.DestroyImmediate(converted);
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
