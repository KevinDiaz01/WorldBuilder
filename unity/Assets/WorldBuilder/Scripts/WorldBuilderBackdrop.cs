using UnityEngine;

namespace WorldBuilder {
 // Editor-only atmosphere. Never serialized or included in VRChat exports.
 // Gives an unfinished build a friendly landscaped stage rather than an empty blue void.
 public sealed class WorldBuilderBackdrop : MonoBehaviour {
  private GameObject ground;
  private Material groundMaterial;
  private Texture2D grassTexture;
  private Material skyMaterial;
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
   Shader shader=Shader.Find("Standard");
   if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
   if(shader==null)shader=Shader.Find("Unlit/Texture");
   groundMaterial=new Material(shader);
   grassTexture=CreateGrassTexture(160);
   grassTexture.wrapMode=TextureWrapMode.Repeat;
   grassTexture.filterMode=FilterMode.Bilinear;
   groundMaterial.mainTexture=grassTexture;
   groundMaterial.mainTextureScale=new Vector2(26f,26f);
   var renderer=ground.GetComponent<Renderer>();
   if(renderer!=null)renderer.sharedMaterial=groundMaterial;
   Shader skyShader=Shader.Find("Skybox/Procedural");
   if(skyShader!=null){
    skyMaterial=new Material(skyShader);
    skyMaterial.SetFloat("_AtmosphereThickness",.85f);
    skyMaterial.SetFloat("_SunSize",.032f);
   }
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
  public void ApplySceneMood(int preset,Camera sceneCamera){
   if(ground==null)Initialize();
   Color[] colors={
    new Color(.61f,.78f,.94f),new Color(.91f,.62f,.45f),
    new Color(.075f,.12f,.24f),new Color(.77f,.80f,.86f)};
   Color tint=colors[Mathf.Clamp(preset,0,colors.Length-1)];
   if(skyMaterial!=null){
    skyMaterial.SetColor("_SkyTint",tint);
    skyMaterial.SetColor("_GroundColor",new Color(.44f,.48f,.40f));
    skyMaterial.SetFloat("_Exposure",preset==2?.36f:preset==1?.85f:1.15f);
    RenderSettings.skybox=skyMaterial;
    if(sceneCamera!=null)sceneCamera.clearFlags=CameraClearFlags.Skybox;
   }else if(sceneCamera!=null){
    sceneCamera.clearFlags=CameraClearFlags.SolidColor;
    sceneCamera.backgroundColor=tint;
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
  }
 }
}