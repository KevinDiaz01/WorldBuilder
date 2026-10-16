#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace WorldBuilder {
 public static class WorldBuilderSetup {
  [MenuItem("Tools/WorldBuilder/Create 3D Prototype Scene")]
  public static void CreateScene(){
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var cameraObject=new GameObject("WorldBuilder Camera");
   cameraObject.tag="MainCamera";
   var cam=cameraObject.AddComponent<Camera>();
   cameraObject.AddComponent<AudioListener>();
   cameraObject.AddComponent<OrbitCamera>();
   var root=new GameObject("WorldBuilder");
   var app=root.AddComponent<WorldBuilderRuntime>();app.sceneCamera=cam;
   var path="Assets/WorldBuilder/WorldBuilderPrototype.unity";
   EditorSceneManager.SaveScene(scene,path);
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
   Selection.activeGameObject=root;
   Debug.Log("WorldBuilder prototype ready. Press Play. Scene: "+path);
  }
 }
}
#endif
