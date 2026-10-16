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
   // Runtime creates the single artistic sun and procedural meadow.
   // Do not add a second light or an opaque ground plane here.
   var root=new GameObject("WorldBuilder");
   var runtime=root.AddComponent<WorldBuilderRuntime>();
   runtime.sceneCamera=camera;
   WorldBuilderRegressionChecks.Run(runtime);
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
