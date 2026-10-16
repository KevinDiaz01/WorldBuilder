using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder {
 // Renders the actual imported model into a small catalog preview image.
 public static class ThumbnailUtility {
  public static void Save(GameObject model,string path){
   if(model==null)return;
   var renderers=model.GetComponentsInChildren<Renderer>(true);
   if(renderers.Length==0)return;
   Bounds bounds=renderers[0].bounds;
   foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   if(bounds.size.sqrMagnitude<.00001f)return;
   const int previewLayer=30;
   var oldLayers=new Dictionary<GameObject,int>();
   foreach(Transform t in model.GetComponentsInChildren<Transform>(true)){
    oldLayers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=previewLayer;
   }
   GameObject cameraObject=null,lightObject=null;
   RenderTexture rt=null;Texture2D image=null;
   RenderTexture previous=RenderTexture.active;
   try{
    cameraObject=new GameObject("WorldBuilder Thumbnail Camera");
    Camera cam=cameraObject.AddComponent<Camera>();
    cam.enabled=false;cam.orthographic=true;cam.aspect=1.35f;
    cam.clearFlags=CameraClearFlags.SolidColor;
    cam.backgroundColor=new Color(.12f,.19f,.25f,1f);
    cam.cullingMask=1<<previewLayer;
    Vector3 direction=new Vector3(.9f,.58f,-1f).normalized;
    cam.transform.position=bounds.center+direction*Mathf.Max(3f,bounds.size.magnitude*2f);
    cam.transform.LookAt(bounds.center);
    cam.orthographicSize=Mathf.Max(.5f,Mathf.Max(bounds.extents.y,bounds.extents.magnitude*.62f));
    cam.nearClipPlane=.01f;cam.farClipPlane=1000f;
    lightObject=new GameObject("WorldBuilder Thumbnail Light");
    var light=lightObject.AddComponent<Light>();
    light.type=LightType.Directional;light.intensity=1.5f;
    light.cullingMask=1<<previewLayer;
    lightObject.transform.rotation=Quaternion.Euler(40,-35,0);
    rt=new RenderTexture(256,192,16,RenderTextureFormat.ARGB32);
    rt.Create();
    cam.targetTexture=rt;
    cam.Render();
    RenderTexture.active=rt;
    image=new Texture2D(256,192,TextureFormat.RGB24,false);
    image.ReadPixels(new Rect(0,0,256,192),0,0);
    image.Apply();
    File.WriteAllBytes(path,image.EncodeToPNG());
    cam.targetTexture=null;
   }catch(Exception error){
    Debug.LogWarning("Thumbnail generation failed: "+error.Message);
   }finally{
    RenderTexture.active=previous;
    foreach(var entry in oldLayers)if(entry.Key!=null)entry.Key.layer=entry.Value;
    if(image!=null)UnityEngine.Object.Destroy(image);
    if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}
    if(cameraObject!=null)UnityEngine.Object.Destroy(cameraObject);
    if(lightObject!=null)UnityEngine.Object.Destroy(lightObject);
   }
  }
 }
}