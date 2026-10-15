#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace WorldBuilder {
 public static class WorldBuilderBuild {
  public static void BuildWindows() {
   // Noninteractive scene creation: safe for Unity -batchmode.
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var cameraObject=new GameObject("WorldBuilder Camera");
   cameraObject.tag="MainCamera";
   var camera=cameraObject.AddComponent<Camera>();
   cameraObject.AddComponent<AudioListener>();
   cameraObject.AddComponent<OrbitCamera>();
   cameraObject.AddComponent<WalkthroughCamera>();
   var sunObject=new GameObject("Sun");
   var sun=sunObject.AddComponent<Light>();
   sun.type=LightType.Directional;
   sun.intensity=1.05f;
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
   RenderSettings.ambientLight=new Color(.60f,.66f,.72f);
   sunObject.transform.rotation=Quaternion.Euler(48,-32,0);
   var root=new GameObject("WorldBuilder");
   var runtime=root.AddComponent<WorldBuilderRuntime>();
   runtime.sceneCamera=camera;
   var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
   ground.name="Ground";
   ground.transform.localScale=new Vector3(10,1,10);
   ground.transform.position=new Vector3(0,-.03f,0);
   var groundMat=new Material(Shader.Find("Standard"));
   groundMat.color=new Color(.27f,.33f,.40f);
   ground.GetComponent<Renderer>().sharedMaterial=groundMat;
   const string scenePath="Assets/WorldBuilder/WorldBuilderPrototype.unity";
   if(!EditorSceneManager.SaveScene(scene,scenePath))
    throw new Exception("Could not save prototype scene");
   Directory.CreateDirectory("Build");
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
    scenes=new[]{scenePath},
    locationPathName="Build/WorldBuilder.exe",
    target=BuildTarget.StandaloneWindows64,
    options=BuildOptions.None
   });
   if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
    throw new Exception("Windows build failed: "+report.summary.result);
   Debug.Log("WorldBuilder Windows prototype built successfully.");
  }
 }
}
#endif
