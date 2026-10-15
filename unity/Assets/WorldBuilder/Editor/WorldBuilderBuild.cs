#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace WorldBuilder {
 public static class WorldBuilderBuild {
  public static void BuildWindows() {
   WorldBuilderSetup.CreateScene();
   const string output = "Build/WorldBuilder.exe";
   Directory.CreateDirectory("Build");
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
    scenes=new[]{"Assets/WorldBuilder/WorldBuilderPrototype.unity"},
    locationPathName=output,
    target=BuildTarget.StandaloneWindows64,
    options=BuildOptions.None
   });
   if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
    throw new Exception("Windows build failed: "+report.summary.result);
   Debug.Log("WorldBuilder built at "+output);
  }
 }
}
#endif
