using UnityEngine;

namespace WorldBuilder {
 // Editor-only atmosphere. Never serialized or included in VRChat exports.
 // Gives an unfinished build a friendly landscaped stage rather than an empty blue void.
 public sealed class WorldBuilderBackdrop : MonoBehaviour {
  private GameObject ground;
  private Material groundMaterial;
  private Texture2D grassTexture;
  private Material skyMaterial;
  private Material panoramaMaterial;
  private Texture2D panoramaTexture;
  private string loadedSkyId="";
  private bool visible=true;
  public bool Visible {get{return visible;}}
  public void Initialize(){
   if(ground!=null)return;
   ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
   ground.name="Scenic ground (editor only)";
   ground.transform.SetParent(transform,false);
   ground.transform.position=new Vector3(0,-.12f,0);
   ground.transform.localScale=new Vector3(22f,1f,22f);
   Collider collider=ground.GetComponent<Collider>();
   if(collider!=null)Destroy(collider);
   // Shaders referenced only through Shader.Find get stripped in standalone builds.
   groundMaterial=new Material(WorldBuilderShaders.Lit);
   grassTexture=CreateGrassTexture(160);
   grassTexture.wrapMode=TextureWrapMode.Repeat;
   grassTexture.filterMode=FilterMode.Bilinear;
   groundMaterial.mainTexture=grassTexture;
   groundMaterial.mainTextureScale=new Vector2(26f,26f);
   var renderer=ground.GetComponent<Renderer>();
   if(renderer!=null)renderer.sharedMaterial=groundMaterial;
   // Use our own packaged gradient instead of Unity's stripped procedural sky.
   // If shader import fails, ApplySceneMood safely uses the camera background.
   Shader skyShader=Resources.Load<Shader>("WorldBuilderSky");
   if(skyShader!=null&&skyShader.isSupported)skyMaterial=new Material(skyShader);
   Shader hdrShader=Resources.Load<Shader>("WorldBuilderPanoramaSky");
   if(hdrShader!=null&&hdrShader.isSupported)panoramaMaterial=new Material(hdrShader);
  }
  static Texture2D CreateGrassTexture(int size){
   var image=new Texture2D(size,size,TextureFormat.RGB24,true);
   image.name="WorldBuilder Meadow";
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    float fine=Mathf.PerlinNoise(x*.20f+12.3f,y*.20f+37.9f);
    float broad=Mathf.PerlinNoise(x*.035f+100.1f,y*.035f+240.7f);
    float mix=.65f*fine+.35f*broad;
    Color a=new Color(.32f,.46f,.27f);
    Color b=new Color(.51f,.64f,.38f);
    image.SetPixel(x,y,Color.Lerp(a,b,Mathf.Clamp01(.12f+.78f*mix)));
   }
   image.Apply(true);return image;
  }
  public void ApplySceneMood(int preset,Camera sceneCamera,string skyId=null){
   if(ground==null)Initialize();
   int mood=Mathf.Clamp(preset,0,3);
   Color[] top={
    new Color(.24f,.51f,.83f),new Color(.16f,.32f,.57f),
    new Color(.012f,.025f,.065f),new Color(.46f,.54f,.63f)};
   Color[] horizon={
    new Color(.72f,.84f,.95f),new Color(.95f,.58f,.37f),
    new Color(.10f,.14f,.24f),new Color(.71f,.75f,.79f)};
   Color[] below={
    new Color(.51f,.61f,.66f),new Color(.45f,.39f,.42f),
    new Color(.035f,.052f,.082f),new Color(.49f,.52f,.55f)};
   // Retain existing gradient presets for older projects and unsupported files.
   string requested=skyId??"";
   if(loadedSkyId!=requested){
    if(panoramaTexture!=null){Destroy(panoramaTexture);panoramaTexture=null;}
    loadedSkyId=requested;
    if(requested.Length>0&&panoramaMaterial!=null){
     try{panoramaTexture=SkyboxLibrary.LoadTexture(requested);}
     catch(System.Exception ex){Diagnostics.Add("WARN","Custom sky could not load: "+ex.Message);}
    }
   }
   if(panoramaMaterial!=null&&panoramaTexture!=null){
    panoramaMaterial.SetTexture("_MainTex",panoramaTexture);
    panoramaMaterial.SetFloat("_Exposure",1f);
    RenderSettings.skybox=panoramaMaterial;
    if(sceneCamera!=null)sceneCamera.clearFlags=CameraClearFlags.Skybox;
   }else if(skyMaterial!=null){
    skyMaterial.SetColor("_SkyTop",top[mood]);
    skyMaterial.SetColor("_SkyHorizon",horizon[mood]);
    skyMaterial.SetColor("_SkyGround",below[mood]);
    RenderSettings.skybox=skyMaterial;
    if(sceneCamera!=null)sceneCamera.clearFlags=CameraClearFlags.Skybox;
   }else if(sceneCamera!=null){
    RenderSettings.skybox=null;
    sceneCamera.clearFlags=CameraClearFlags.SolidColor;
    sceneCamera.backgroundColor=horizon[mood];
   }
   if(groundMaterial!=null)groundMaterial.color=preset==2?
    new Color(.52f,.59f,.67f):Color.white;
  }
  public void SetVisible(bool enabled){
   visible=enabled;
   if(ground!=null)ground.SetActive(enabled);
  }
  void OnDestroy(){
   if(ground!=null)Destroy(ground);
   if(groundMaterial!=null)Destroy(groundMaterial);
   if(grassTexture!=null)Destroy(grassTexture);
   if(skyMaterial!=null)Destroy(skyMaterial);
   if(panoramaMaterial!=null)Destroy(panoramaMaterial);
   if(panoramaTexture!=null)Destroy(panoramaTexture);
  }
 }
}