using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WorldBuilder {
 [Serializable] public class Piece {
  public string kind;
  public Vector3 position;
  public Vector3 scale;
  public Quaternion rotation;
  public string assetId;
  public bool fitToSurface;
  public string roomId;
  public string groupId;
  public string finish;
  public List<Vector3> vertices=new List<Vector3>();
  public float lightIntensity=1.5f;
  public float lightRange=10f;
  public float lightAngle=55f;
  public Color lightColor=Color.white;
  public float textureTile=1f;
  public Piece(string kind, Vector3 position, Vector3 scale, Quaternion rotation) {
   this.kind=kind;this.position=position;this.scale=scale;this.rotation=rotation;
  }
 }
 [Serializable] public class Project {
  public int schema=2;
  public List<Piece> pieces=new List<Piece>();
  public int skyPreset=0;
  // Optional imported environment. Missing field in old schema-2 saves is safe.
  public string skyboxId="";
  public float sunYaw=35f;
  public float sunPitch=48f;
  public bool fog=false;
 }
 public class WorldBuilderRuntime : MonoBehaviour {
  public Camera sceneCamera;
  private WalkthroughCamera walkthrough;
  private float avatarHeight=1.70f;
  public Material wallMaterial;
  public Material floorMaterial;
  public Material furnitureMaterial;
  public float gridSize=1f;
  public float wallHeight=2.7f;
  public float wallThickness=.16f;
  private int buildingLevel=0;
  const float LevelHeight=2.85f;
  const float DefaultWallHeight=2.7f;
  const float DefaultWallThickness=.16f;
  public enum Tool { Wall, Floor, Couch, Table, Erase, Imported, Select, Room, Door, Window, Roof, Stairs, Paint, Garage, Light, Terrain, Water, Mirror, Seat, Spawn, Tree, Path }
  public Tool currentTool=Tool.Wall;
  private Vector3? anchor;
  private bool dragAnchorArmed=false;
  private Vector2 dragAnchorMouse;
  private readonly Stack<string> undoHistory=new Stack<string>();
  private readonly Stack<string> redoHistory=new Stack<string>();
  private bool restoringHistory=false;
  private Project project=new Project();
  private readonly List<GameObject> objects=new List<GameObject>();
  private string filePath;
  private bool showHelp=false;
  private bool showDiagnostics=false;
  private Vector2 diagnosticsScroll;
  private string diagnosticFilter="All";
  private bool showPause=false;
  private bool showConfirmExit=false;
  private bool quitting=false;
  private bool showSettings=false;
  private int selectedIndex=-1;
  private readonly HashSet<int> multiSelection=new HashSet<int>();
  private bool draggingSelection=false;
  private Vector3 selectionDragOffset;
  private LineRenderer selectionOutline;
  private int creativeMode=0;
  private int catalogCategory=0;
  private int furnitureRoom=0;
  private bool textureOnly=false;
  private int landscapeCategory=0;
  private int selectedFinish=0;
  private string selectedTextureId="";
  private float selectedTextureTile=1f;
  private readonly Dictionary<string,Texture2D> materialImages=new Dictionary<string,Texture2D>();
  private readonly Dictionary<string,Texture2D> associatedImages=new Dictionary<string,Texture2D>();
  private static readonly string[] FinishNames={"Default","Warm White","Stone","Charcoal","Sand","Terracotta","Sage","Ocean"};
  private static readonly Color[] FinishColors={Color.white,new Color(.91f,.88f,.80f),new Color(.58f,.62f,.63f),new Color(.19f,.23f,.28f),new Color(.77f,.67f,.51f),new Color(.67f,.34f,.26f),new Color(.38f,.52f,.43f),new Color(.22f,.47f,.62f)};
  private Vector2 catalogScroll;
  private Vector2 catalogTabScroll;
  private bool landscapeObjects=false;
  private Vector2 inspectorScroll;
  private bool inspectorAdvanced=false;
  private float inspectorHeight=0f;
  private string catalogSearch="";
  private int editingPiece=-1;
  private Vector3? copiedWallSize;
  private const float StandardWallHeight=2.7f;
  private readonly string[] dimensionDraft=new string[6];
  private string status="Ready to build";
  private float statusUntil;
  private bool showLibrary=true;
  private bool showCatalog=false;
  // Build/Landscape have left-hand tools; palettes are available on demand.
  private readonly bool[] catalogOpenForMode={false,false,false};
  private bool showInspector=false;
  private bool showProjects=false;
  private GameObject preview;
  private LineRenderer openingGuide;
  private GameObject importedGhost;
  private string ghostAssetId="";
  private bool furnitureSnap=true;
  private Rect objectHandleArea;
  private bool objectHandleVisible=false;
  private float nextRecoveryTime=0f;
  private const float RecoveryInterval=30f;
  private bool recoverySessionStarted=false;
  private RecoverySnapshot interruptedRecovery;
  private bool showRecoveryPrompt=false;
  private string importedId;
  private float importedScale=1f;
  private float importedFitBase=1f;
  private float importedRotation=0f;
  private Bounds importedBounds;
  private bool importedBoundsReady=false;
  private string importPath="";
  private string importError="";
  private string targetVrcProject="";
  private bool showImport=false;
  private bool showAssets=false;
  private System.Diagnostics.Process browserProcess;
  private PolyHavenCatalog polyHaven;
  private string assetSearch="";
  private int sourceTab=0;
  private float nextAssetScan=0f;
  private string assetStatus="Automatically detecting OBJ, GLB, glTF and ZIP in asset folders";
  private Vector2 assetScroll;
  private bool showExport=false;
  private bool showGrid=true;
  private bool polygonMode=false;
  private readonly List<Vector3> polygonCorners=new List<Vector3>();
  private float openingWidth=1.2f;
  private float openingHeight=2.1f;
  private float openingBottom=0f;
  private bool showStudio=false;
  private int studioTab=0;
  private int lightMode=0;
  private float lightBrightness=.9f;
  private float lightRadius=7f;
  private float terrainAmplitude=2.5f;
  private float newWaterDepth=1.5f;
  private int basinFingerprint=int.MinValue;
  private Light editorSun;
  private WorldBuilderBackdrop scenicBackdrop;
  private readonly List<GameObject> gridMarkers=new List<GameObject>();
  // Camera movement and scroll should never consume input over a tray or modal.
  public bool BlocksCameraPointer(){return OverUI();}
  bool OverUI(){
   if(walkthrough!=null&&walkthrough.IsWalking)return true;
   if(showDiagnostics)return true;
   float scale=UIScale;
   float w=Screen.width/scale,h=Screen.height/scale;
   Vector2 p=new Vector2(Input.mousePosition.x/scale,(Screen.height-Input.mousePosition.y)/scale);
   return (objectHandleVisible&&objectHandleArea.Contains(p))||p.y<88f||(showCatalog&&p.y>h-CatalogHeight(h)-14f)||
     (!showCatalog&&p.y>h-79f&&p.x<523f)||
     (showLibrary&&p.x<122f&&p.y>=94f&&p.y<735f)||
     (showInspector&&p.x>w-373f&&p.y>=94f&&p.y<(showCatalog?h-CatalogHeight(h)-6f:h-90f))||
     (p.x>125f&&p.x<842f&&p.y<159f)||
     (p.x>=135f&&p.x<=825f&&
      p.y>=h-(showCatalog?CatalogHeight(h)+60f:119f)&&
      p.y<=h-(showCatalog?CatalogHeight(h)+8f:68f))||
     showPause||showConfirmExit||showSettings||showHelp||
     (showProjects&&p.x>w*.5f-200f&&p.x<w*.5f+210f&&p.y<505f)||
     ((showImport||showExport)&&p.x>w*.5f-275f&&p.x<w*.5f+275f&&p.y<335f)||
     showAssets||(showStudio&&p.x>515f&&p.x<1165f&&p.y>90f&&p.y<455f);
  }
  void ToggleAssetBrowser(){ToggleAssetBrowser("Models");}
  void ToggleAssetBrowser(string category){
   try{
    if(browserProcess!=null&&!browserProcess.HasExited){
     // Changing asset categories should reopen the browser for the new search,
     // rather than silently closing it.
     browserProcess.Kill();browserProcess.Dispose();browserProcess=null;
     showAssets=false;
    }
    string exe=Path.Combine(Application.dataPath,"AssetBrowser","WorldBuilderAssetBrowser.exe");
    if(!File.Exists(exe)){Notice("Asset browser component is missing. Reinstall the complete update.");return;}
    IntPtr hwnd=System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    if(hwnd==IntPtr.Zero){Notice("Unable to locate WorldBuilder window");return;}
    if(browserProcess!=null){browserProcess.Dispose();browserProcess=null;}
    browserProcess=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe,
      hwnd.ToInt64().ToString()+" "+QuoteArg(AssetHub.Root)+" "+QuoteArg(category)){UseShellExecute=false});
    showAssets=true;
    Notice("Assets browser opening...");
   }catch(Exception ex){Notice("Assets browser failed: "+ex.Message);UnityEngine.Debug.LogException(ex);}
  }
  string QuoteArg(string value){return "\""+value.Replace("\"","")+"\"";}
  void OnEnable(){Application.logMessageReceived+=CaptureLog;}
  void OnDisable(){Application.logMessageReceived-=CaptureLog;}
  void CaptureLog(string message,string stack,LogType type){
   if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
   if(message.StartsWith("Asset Hub:",StringComparison.Ordinal))return;
   string brief=message.Length>400?message.Substring(0,400)+"...":message;
   Diagnostics.Add("ERROR",brief);
  }
  void OnApplicationQuit(){
   if(recoverySessionStarted){
    WriteRecovery();
    try{RecoveryStore.EndSession();}catch(Exception ex){Debug.LogWarning("Recovery session cleanup: "+ex.Message);}
    recoverySessionStarted=false;
   }
   if(importedGhost!=null)Destroy(importedGhost);
   if(scenicBackdrop!=null)Destroy(scenicBackdrop.gameObject);
   if(browserProcess!=null&&!browserProcess.HasExited)browserProcess.Kill();
   foreach(var tex in modelThumbnails.Values)if(tex!=null)Destroy(tex);
   modelThumbnails.Clear();
   foreach(var tex in materialImages.Values)if(tex!=null)Destroy(tex);
   materialImages.Clear();
   foreach(var tex in associatedImages.Values)if(tex!=null)Destroy(tex);
   associatedImages.Clear();
  }
  void SelectObject(int index){
   selectedIndex=index;
   multiSelection.Clear();
   if(index<0)objectHandleVisible=false;
   if(index>=0&&index<project.pieces.Count){
    showInspector=true;
    string group=project.pieces[index].groupId;
    if(!string.IsNullOrEmpty(group)){
     for(int i=0;i<project.pieces.Count;i++)
      if(project.pieces[i].groupId==group)multiSelection.Add(i);
    }else multiSelection.Add(index);
   }
   editingPiece=-1;
   RefreshSelectionOutline();
  }
  void ToggleMultiple(int index){
   if(index<0||index>=project.pieces.Count)return;
   if(multiSelection.Contains(index)){
    multiSelection.Remove(index);
    if(multiSelection.Count==0){SelectObject(-1);return;}
    foreach(int remaining in multiSelection){selectedIndex=remaining;break;}
   }else{multiSelection.Add(index);selectedIndex=index;}
   showInspector=true;editingPiece=-1;RefreshSelectionOutline();
   Notice(multiSelection.Count+" objects selected");
  }
  void GroupSelected(){
   if(multiSelection.Count<2){Notice("Shift-click two or more objects to group");return;}
   RememberChange();
   string group=Guid.NewGuid().ToString("N");
   foreach(int i in multiSelection)if(i>=0&&i<project.pieces.Count)project.pieces[i].groupId=group;
   Notice("Grouped "+multiSelection.Count+" objects · select one to move all");
  }
  void UngroupSelected(){
   if(multiSelection.Count==0)return;
   RememberChange();
   foreach(int i in multiSelection)if(i>=0&&i<project.pieces.Count)project.pieces[i].groupId=null;
   Notice("Objects ungrouped");
  }

  void RefreshSelectionOutline(){
   if(selectionOutline==null){
    var helper=new GameObject("WorldBuilder Selection Outline");
    selectionOutline=helper.AddComponent<LineRenderer>();
    selectionOutline.material=new Material(WorldBuilderShaders.Overlay);
    selectionOutline.startWidth=.025f;selectionOutline.endWidth=.025f;
    selectionOutline.startColor=selectionOutline.endColor=new Color(.18f,.77f,1f,1f);
    selectionOutline.useWorldSpace=true;
   }
   bool valid=selectedIndex>=0&&selectedIndex<objects.Count&&objects[selectedIndex]!=null;
   selectionOutline.enabled=valid;
   if(!valid)return;
   var renderers=objects[selectedIndex].GetComponentsInChildren<Renderer>();
   if(renderers.Length==0)return;
   Bounds b=renderers[0].bounds;
   for(int k=1;k<renderers.Length;k++)b.Encapsulate(renderers[k].bounds);
   Vector3 min=b.min,max=b.max;
   Vector3[] corners={
    new Vector3(min.x,min.y,min.z),new Vector3(max.x,min.y,min.z),
    new Vector3(max.x,min.y,max.z),new Vector3(min.x,min.y,max.z),
    new Vector3(min.x,min.y,min.z),new Vector3(min.x,max.y,min.z),
    new Vector3(max.x,max.y,min.z),new Vector3(max.x,min.y,min.z),
    new Vector3(max.x,max.y,min.z),new Vector3(max.x,max.y,max.z),
    new Vector3(max.x,min.y,max.z),new Vector3(max.x,max.y,max.z),
    new Vector3(min.x,max.y,max.z),new Vector3(min.x,min.y,max.z),
    new Vector3(min.x,max.y,max.z),new Vector3(min.x,max.y,min.z)};
   selectionOutline.positionCount=corners.Length;
   selectionOutline.SetPositions(corners);
  }
  void DuplicateSelection(){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count)return;
   Piece original=project.pieces[selectedIndex];
   var copy=new Piece(original.kind,original.position+new Vector3(gridSize,0,gridSize),original.scale,original.rotation);
   copy.assetId=original.assetId;
   // Duplicate a piece as an independent object rather than joining its former room.
   copy.roomId=null;
   copy.groupId=null;
   copy.finish=original.finish;
   copy.textureTile=original.textureTile;
   Add(copy);
   SelectObject(project.pieces.Count-1);
   Notice("Object duplicated. Drag to reposition.");
  }
  void NudgeSelection(Vector3 delta){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count||objects[selectedIndex]==null)return;
   if(IsRoomFloor(selectedIndex)){Notice("Room groups move using drag-select");return;}
   RememberChange();
   var piece=project.pieces[selectedIndex];
   piece.position+=delta;
   objects[selectedIndex].transform.position=piece.position;
   editingPiece=-1;
   RefreshSelectionOutline();
  }
  void RotateSelected(float degrees){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count||objects[selectedIndex]==null)return;
   if(IsRoomFloor(selectedIndex)){Notice("Room rotation is not available");return;}
   RememberChange();
   var piece=project.pieces[selectedIndex];
   piece.rotation=Quaternion.Euler(0f,degrees,0f)*piece.rotation;
   objects[selectedIndex].transform.rotation=piece.rotation;
   RefreshSelectionOutline();
  }
  void FitSelectedImportedModel(){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count)return;
   Piece item=project.pieces[selectedIndex];
   if(item.kind!="imported"||string.IsNullOrEmpty(item.assetId))return;
   GameObject sample=null;
   try{
    sample=ImportedObj.Load(Path.Combine(AssetLibrary(),item.assetId,"model.obj"));
    Bounds bounds=ModelBounds(sample);
    float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
    if(largest<.001f)throw new InvalidDataException("No usable model bounds");
    float previousScale=item.scale.y;
    float floorY=item.fitToSurface?item.position.y:item.position.y+bounds.min.y*previousScale;
    RememberChange();
    item.fitToSurface=true;
    item.position.y=Mathf.Clamp(floorY,-10f,100f);
    float fit=Mathf.Clamp(2f/largest,.0001f,20f);
    item.scale=Vector3.one*fit;
    if(objects[selectedIndex]!=null)Destroy(objects[selectedIndex]);
    objects[selectedIndex]=Create(item);
    editingPiece=-1;
    RefreshSelectionOutline();
    Notice("Model fitted to 2 m and aligned to the floor");
   }catch(Exception ex){
    Diagnostics.Add("ERROR","Model auto-fit failed: "+ex.Message);
    Notice("Could not fit model · see F2");
   }finally{if(sample!=null)Destroy(sample);}
  }
  void ResizeSelected(float factor){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count||objects[selectedIndex]==null)return;
   string type=project.pieces[selectedIndex].kind;
   if(type=="wall"||type=="floor"||type=="roof"||type=="opening-frame"){
    Notice("Use dimension fields to resize architectural pieces");return;
   }
   RememberChange();
   var piece=project.pieces[selectedIndex];
   piece.scale*=factor;
   objects[selectedIndex].transform.localScale=piece.scale;
   editingPiece=-1;
   RefreshSelectionOutline();
  }
  void DrawObjectHandles(){
   objectHandleVisible=false;
   if(currentTool!=Tool.Select||selectedIndex<0||selectedIndex>=objects.Count||objects[selectedIndex]==null||sceneCamera==null)return;
   if(showPause||showHelp||showSettings||showDiagnostics||showImport||showExport||showProjects)return;
   var renderers=objects[selectedIndex].GetComponentsInChildren<Renderer>();
   if(renderers.Length==0)return;
   Bounds b=renderers[0].bounds;
   for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
   Vector3 world=b.center+Vector3.up*(b.extents.y+.4f);
   Vector3 screen=sceneCamera.WorldToScreenPoint(world);
   if(screen.z<=0f)return;
   float px=screen.x/UIScale,py=(Screen.height-screen.y)/UIScale;
   if(px<130f||py<92f||px>Screen.width/UIScale-370f||py>Screen.height/UIScale-290f)return;
   objectHandleArea=new Rect(px-92,py-65,184,120);
   objectHandleVisible=true;
   GUI.Box(objectHandleArea,"",panelStyle);
   Label(new Rect(px-78,py-56,165,25),"MOVE · 0.25 m");
   if(Button(new Rect(px-88,py-25,53,34),"X−"))NudgeSelection(new Vector3(-.25f,0,0));
   if(Button(new Rect(px-30,py-25,53,34),"X+"))NudgeSelection(new Vector3(.25f,0,0));
   if(Button(new Rect(px+28,py-25,53,34),"Y+"))NudgeSelection(new Vector3(0,.25f,0));
   if(Button(new Rect(px-88,py+13,53,34),"Z−"))NudgeSelection(new Vector3(0,0,-.25f));
   if(Button(new Rect(px-30,py+13,53,34),"Z+"))NudgeSelection(new Vector3(0,0,.25f));
   if(Button(new Rect(px+28,py+13,53,34),"Y−"))NudgeSelection(new Vector3(0,-.25f,0));
  }
  void RotateSelection(){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count)return;
   var item=project.pieces[selectedIndex];
   RememberChange();
   item.rotation=Quaternion.Euler(0,45,0)*item.rotation;
   objects[selectedIndex].transform.rotation=item.rotation;
   RefreshSelectionOutline();
   Notice("Rotated 45 degrees");
  }
  void UpdateWallSize(int index,Vector3 requestedSize){
   if(index<0||index>=project.pieces.Count||project.pieces[index].kind!="wall")return;
   Piece wall=project.pieces[index];
   Vector3 size=new Vector3(Mathf.Max(.1f,requestedSize.x),Mathf.Max(.1f,requestedSize.y),
     Mathf.Max(.04f,requestedSize.z));
   RememberChange();
   // Keep the bottom of the wall at the same height as the dimensions change.
   float baseY=wall.position.y-wall.scale.y*.5f;
   wall.scale=size;
   wall.position.y=baseY+size.y*.5f;
   if(objects[index]!=null)Destroy(objects[index]);
   objects[index]=Create(wall);
   editingPiece=-1;
   RefreshSelectionOutline();
  }
  void UseSelectedWallForBuilding(){
   if(selectedIndex<0||selectedIndex>=project.pieces.Count||project.pieces[selectedIndex].kind!="wall")return;
   var wall=project.pieces[selectedIndex];
   wallHeight=wall.scale.y;
   wallThickness=wall.scale.z;
   buildingLevel=Mathf.Clamp(Mathf.RoundToInt((wall.position.y-wall.scale.y*.5f)/LevelHeight),0,8);
   SelectObject(-1);SelectTool(Tool.Wall);
   Notice("New walls will use the selected wall height and thickness. Reset to restore defaults.");
  }
  void ResetWallDefaults(){wallHeight=DefaultWallHeight;wallThickness=DefaultWallThickness;buildingLevel=0;anchor=null;Notice("Default wall: 2.70 m tall, 0.16 m thick, ground level");}
  void SetStandardWallHeight(){
   if(selectedIndex<0||project.pieces[selectedIndex].kind!="wall")return;
   Vector3 size=project.pieces[selectedIndex].scale;
   size.y=StandardWallHeight;
   UpdateWallSize(selectedIndex,size);
   Notice("Wall set to standard 2.70 m height");
  }
  void CopyWallSize(){
   if(selectedIndex<0||project.pieces[selectedIndex].kind!="wall")return;
   copiedWallSize=project.pieces[selectedIndex].scale;
   Notice("Wall dimensions copied. Select another wall and paste.");
  }
  void PasteWallSize(){
   if(!copiedWallSize.HasValue){Notice("Copy a wall size first");return;}
   if(selectedIndex<0||project.pieces[selectedIndex].kind!="wall")return;
   UpdateWallSize(selectedIndex,copiedWallSize.Value);
   Notice("Wall size applied (length, height and thickness)");
  }
  void DeleteSelection(){
   if(selectedIndex<0||selectedIndex>=objects.Count)return;
   RememberChange();
   if(objects[selectedIndex]!=null)Destroy(objects[selectedIndex]);
   objects.RemoveAt(selectedIndex);
   project.pieces.RemoveAt(selectedIndex);
   SelectObject(-1);
   Notice("Object deleted");
  }
  void RememberChange(){
   if(restoringHistory)return;
   // Save the result shortly after an edit; the normal 30-second interval
   // remains a backup for sliders and other operations that bypass history.
   if(recoverySessionStarted&&!showRecoveryPrompt)
    nextRecoveryTime=Mathf.Min(nextRecoveryTime,Time.unscaledTime+5f);
   undoHistory.Push(JsonUtility.ToJson(project));
   if(undoHistory.Count>80){
    var snapshots=undoHistory.ToArray();
    undoHistory.Clear();
    for(int i=Mathf.Min(79,snapshots.Length-1);i>=0;i--)undoHistory.Push(snapshots[i]);
   }
   redoHistory.Clear();
  }
  void RestoreSnapshot(string json){
   var restored=JsonUtility.FromJson<Project>(json);
   if(restored==null||restored.pieces==null)return;
   restoringHistory=true;
   SelectObject(-1);
   for(int k=0;k<objects.Count;k++)if(objects[k]!=null)Destroy(objects[k]);
   objects.Clear();
   project=restored;
   foreach(var part in project.pieces)objects.Add(Create(part));
   ApplyEnvironment();
   anchor=null;
   restoringHistory=false;
  }
  void UndoLast(){
   if(undoHistory.Count==0){Notice("Nothing to undo");return;}
   redoHistory.Push(JsonUtility.ToJson(project));
   RestoreSnapshot(undoHistory.Pop());
   Notice("Undone");
  }
  void RedoLast(){
   if(redoHistory.Count==0){Notice("Nothing to redo");return;}
   undoHistory.Push(JsonUtility.ToJson(project));
   RestoreSnapshot(redoHistory.Pop());
   Notice("Redone");
  }
  void ToggleGrid(){
   showGrid=!showGrid;
   foreach(var obj in gridMarkers)if(obj!=null)Destroy(obj);
   gridMarkers.Clear();
   if(showGrid)BuildGrid();
   Notice("Grid "+(showGrid?"visible":"hidden"));
  }
  void BuildGrid(){
   for(int i=-20;i<=20;i++){
    for(int axis=0;axis<2;axis++){
     var go=new GameObject("Grid");
     var line=go.AddComponent<LineRenderer>();
     line.material=new Material(WorldBuilderShaders.Overlay);
     line.positionCount=2;line.startWidth=.012f;line.endWidth=.012f;
     line.startColor=line.endColor=new Color(.58f,.64f,.70f,.19f);
     line.SetPosition(0,axis==0?new Vector3(-20,.012f,i):new Vector3(i,.012f,-20));
     line.SetPosition(1,axis==0?new Vector3(20,.012f,i):new Vector3(i,.012f,20));
     gridMarkers.Add(go);
    }
   }
  }
  // Give floor surfaces clearance above the base terrain to prevent z-fighting.
  const float FloorTop=0.18f;
  const float FloorThickness=0.15f;
  const float FloorCenter=FloorTop-FloorThickness*.5f;
  void SelectImportedAsset(string id){
   importedId=id;importedBoundsReady=false;
   try{
    GameObject sample=ImportedObj.Load(Path.Combine(AssetLibrary(),id,"model.obj"));
    var renderers=sample.GetComponentsInChildren<Renderer>();
    if(renderers.Length>0){
     importedBounds=renderers[0].bounds;
     foreach(var renderer in renderers)importedBounds.Encapsulate(renderer.bounds);
     importedBoundsReady=true;
     float largest=Mathf.Max(importedBounds.size.x,Mathf.Max(importedBounds.size.y,importedBounds.size.z));
     if(largest>.001f){
      importedFitBase=Mathf.Clamp(2f/largest,.0001f,20f);
      importedScale=importedFitBase;
      if(largest>12f||largest<.05f)
       Diagnostics.Add("WARN","Model units normalized for "+AssetHub.Name(id)+
        " (original "+largest.ToString("F2")+" m; placement "+importedScale.ToString("F3")+"x)");
     }
    }
    Destroy(sample);
   }catch(Exception error){Diagnostics.Add("ERROR","Unable to read asset bounds: "+error.Message);}
   SelectTool(Tool.Imported);
   Notice("Placing "+AssetHub.Name(id)+" · R/E rotate · click to place");
  }
  Vector3 PlacementSurface(Vector3 ground){
   if(sceneCamera==null)return ground;
   Ray ray=sceneCamera.ScreenPointToRay(Input.mousePosition);
   RaycastHit[] hits=Physics.RaycastAll(ray,500f);
   Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   foreach(var hit in hits){
    for(int i=0;i<objects.Count;i++){
     if(objects[i]==null||(project.pieces[i].kind!="floor"&&project.pieces[i].kind!="polygon-floor"&&project.pieces[i].kind!="terrain"))continue;
     if(hit.collider.gameObject==objects[i]||hit.collider.transform.IsChildOf(objects[i].transform))
      return hit.point;
    }
   }
   return ground;
  }
  Vector3 ImportedPlacement(Vector3 ground){
   return PlacementSurface(ground);
  }
  void AnchorImportedModel(GameObject root,GameObject model,Bounds bounds){
   model.transform.SetParent(root.transform,false);
   model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
  }
  Bounds ModelBounds(GameObject model){
   var renderers=model.GetComponentsInChildren<Renderer>();
   if(renderers.Length==0)throw new InvalidDataException("Imported model contains no renderers");
   Bounds bounds=renderers[0].bounds;
   for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
   return bounds;
  }
  void UpdateImportedGhost(){
   if(string.IsNullOrEmpty(importedId))return;
   if(importedGhost!=null&&ghostAssetId==importedId)return;
   if(importedGhost!=null)Destroy(importedGhost);
   importedGhost=null;ghostAssetId="";
   try{
    var mesh=ImportedObj.Load(Path.Combine(AssetLibrary(),importedId,"model.obj"));
    importedGhost=new GameObject("Furniture placement preview");
    AnchorImportedModel(importedGhost,mesh,ModelBounds(mesh));
    foreach(var collider in importedGhost.GetComponentsInChildren<Collider>())Destroy(collider);
    foreach(var renderer in importedGhost.GetComponentsInChildren<Renderer>()){
     var original=renderer.sharedMaterials;
     var ghosts=new Material[original.Length];
     for(int i=0;i<original.Length;i++){
      ghosts[i]=new Material(WorldBuilderShaders.Overlay);
      if(original[i]!=null&&original[i].mainTexture!=null)ghosts[i].mainTexture=original[i].mainTexture;
      ghosts[i].color=new Color(.35f,.83f,1f,.72f);
     }
     renderer.materials=ghosts;
    }
    ghostAssetId=importedId;
   }catch(Exception ex){Diagnostics.Add("ERROR","Furniture preview: "+ex.Message);}
  }
  Vector3 SnapFurniture(Vector3 where){
   if(!furnitureSnap||!importedBoundsReady)return where;
   float best=.28f;
   Vector3 adjusted=where;
   // Keep simple wall alignment independent of the grid and avoid changing vertical placement.
   for(int i=0;i<project.pieces.Count;i++){
    var wall=project.pieces[i];
    if(wall.kind!="wall")continue;
    Vector3 center=wall.position;
    Vector3 normal=wall.rotation*Vector3.forward;
    Vector3 tangent=wall.rotation*Vector3.right;
    float along=Vector3.Dot(where-center,tangent);
    if(Mathf.Abs(along)>wall.scale.x*.5f)continue;
    float signed=Vector3.Dot(where-center,normal);
    float halfDepth=importedBounds.extents.z*importedScale;
    float goal=Mathf.Sign(signed)*(wall.scale.z*.5f+halfDepth+.03f);
    float difference=Mathf.Abs(signed-goal);
    if(difference<best){best=difference;adjusted=where+normal*(goal-signed);}
   }
   return adjusted;
  }
  void WriteRecovery(){
   if(!recoverySessionStarted||showRecoveryPrompt||project==null||project.pieces==null)return;
   try{
    RecoveryStore.WriteSnapshot(filePath,JsonUtility.ToJson(project,true),project.pieces.Count);
   }catch(Exception ex){Diagnostics.Add("WARN","Recovery snapshot failed: "+ex.Message);}
  }
  void RestoreRecovery(bool previousCrash=false){
   try{
    RecoverySnapshot candidate=previousCrash?
      (interruptedRecovery??RecoveryStore.ReadPreviousCrash()):
      RecoveryStore.ReadLatest(filePath);
    if(candidate==null){Notice("No recovery snapshot available");showRecoveryPrompt=false;return;}
    var restored=JsonUtility.FromJson<Project>(candidate.projectJson);
    if(restored==null||restored.schema!=2||restored.pieces==null)throw new InvalidDataException("Recovery file is invalid");
    RememberChange();
    // Restore into the editor first, never overwrite the manually saved project.
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();SelectObject(-1);
    project=restored;
    filePath=candidate.sourcePath;
    if(recoverySessionStarted)RecoveryStore.ChangeProject(filePath);
    foreach(var piece in project.pieces)objects.Add(Create(piece));
    ApplyEnvironment();
    showRecoveryPrompt=false;
    showProjects=false;
    interruptedRecovery=null;
    nextRecoveryTime=Time.unscaledTime+RecoveryInterval;
    Notice("Build recovered. Use Save to keep your restored changes.");
    Diagnostics.Add("INFO","Restored recovery snapshot for "+Path.GetFileName(filePath));
   }catch(Exception ex){
    Diagnostics.Add("ERROR","Recovery restore failed: "+ex.Message);
    showRecoveryPrompt=false;
    Notice("Recovery failed. See F2 Diagnostics; backup files have been kept.");
   }
  }
  void OnApplicationFocus(bool hasFocus){
   if(!hasFocus)WriteRecovery();
  }
  void OnApplicationPause(bool paused){
   if(paused)WriteRecovery();
  }
  void UpdatePreview(){
   bool aimedAtWall=(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage)&&FindOpeningWall(out int ignoredWall,out Vector3 ignoredHit);
   bool onGround=MouseOnGround(out Vector3 p);
   if(OverUI()||currentTool==Tool.Erase||currentTool==Tool.Paint||currentTool==Tool.Select||(!onGround&&!aimedAtWall)){
    if(importedGhost!=null)importedGhost.SetActive(false);
    if(preview!=null)preview.SetActive(false);
    return;
   }
   if(preview==null){
    preview=GameObject.CreatePrimitive(PrimitiveType.Cube);
    preview.name="Build cursor";
    Destroy(preview.GetComponent<Collider>());
    var m=new Material(WorldBuilderShaders.Overlay);
    m.color=new Color(.24f,.54f,.91f,.45f);
    preview.GetComponent<Renderer>().material=m;
    openingGuide=preview.AddComponent<LineRenderer>();
    openingGuide.useWorldSpace=false;
    openingGuide.positionCount=5;
    openingGuide.startWidth=openingGuide.endWidth=.053f;
    openingGuide.material=new Material(WorldBuilderShaders.Overlay);
    openingGuide.enabled=false;
   }
   if(currentTool==Tool.Imported){
    preview.SetActive(false);
    UpdateImportedGhost();
    if(importedGhost!=null){
     importedGhost.SetActive(true);
     importedGhost.transform.SetPositionAndRotation(SnapFurniture(ImportedPlacement(p)),Quaternion.Euler(0,importedRotation,0));
     importedGhost.transform.localScale=Vector3.one*importedScale;
    }
    return;
   }
   if(importedGhost!=null)importedGhost.SetActive(false);
   preview.SetActive(true);
   if(openingGuide!=null)openingGuide.enabled=false;
   preview.GetComponent<Renderer>().enabled=true;
   preview.transform.rotation=Quaternion.identity;
   if(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage){
    if(!FindOpeningWall(out int wallIndex,out Vector3 hitPoint)){preview.SetActive(false);return;}
    Piece wall=project.pieces[wallIndex];
    bool window=currentTool==Tool.Window;
    float previewWidth=openingWidth;
    float previewHeight=openingHeight;
    if(wall.scale.x<previewWidth+.18f||wall.scale.y<previewHeight+.12f){
     preview.SetActive(false);return;
    }
    float previewBottom=Mathf.Clamp(openingBottom,0f,wall.scale.y-previewHeight);
    Vector3 local=Quaternion.Inverse(wall.rotation)*(hitPoint-wall.position);
    local.x=Mathf.Clamp(local.x,-wall.scale.x*.5f+previewWidth*.5f+.08f,wall.scale.x*.5f-previewWidth*.5f-.08f);
    local.y=previewBottom+previewHeight*.5f-wall.scale.y*.5f;
    local.z=0;
    Vector3 forward=wall.rotation*Vector3.forward;
    float facing=Vector3.Dot(sceneCamera.transform.position-wall.position,forward)>=0?1f:-1f;
    preview.transform.position=wall.position+wall.rotation*local+forward*(facing*(wall.scale.z*.5f+.04f));
    preview.transform.rotation=wall.rotation;
    preview.transform.localScale=Vector3.one;
    preview.GetComponent<Renderer>().enabled=false;
    if(openingGuide!=null){
     openingGuide.enabled=true;
     float l=-previewWidth*.5f,r=previewWidth*.5f;
     float t=previewHeight*.5f,b=-previewHeight*.5f;
     openingGuide.SetPosition(0,new Vector3(l,b,0));
     openingGuide.SetPosition(1,new Vector3(l,t,0));
     openingGuide.SetPosition(2,new Vector3(r,t,0));
     openingGuide.SetPosition(3,new Vector3(r,b,0));
     openingGuide.SetPosition(4,new Vector3(l,b,0));
     Color line=currentTool==Tool.Garage?new Color(.95f,.68f,.29f,.95f):
       currentTool==Tool.Window?new Color(.34f,.62f,.92f,.95f):new Color(.43f,.75f,.55f,.95f);
     openingGuide.startColor=openingGuide.endColor=line;
    }
   }else if(currentTool==Tool.Light||currentTool==Tool.Mirror||currentTool==Tool.Seat||currentTool==Tool.Spawn){
    float size=currentTool==Tool.Mirror?1.4f:.3f;
    preview.transform.position=p+Vector3.up*(currentTool==Tool.Light?2.35f:currentTool==Tool.Mirror?1.4f:.5f);
    preview.transform.localScale=Vector3.one*size;
   }else if(currentTool==Tool.Tree){
    preview.transform.position=p+Vector3.up*2f;
    preview.transform.localScale=new Vector3(1.3f,4f,1.3f);
   }else if(currentTool==Tool.Couch||currentTool==Tool.Table){
    bool sofa=currentTool==Tool.Couch;
    preview.transform.position=p+Vector3.up*.4f;
    preview.transform.localScale=new Vector3(sofa?2f:1.2f,.8f,sofa?.9f:1.2f);
   }else if(anchor.HasValue){
    Vector3 d=p-anchor.Value;
    if(currentTool==Tool.Wall){
     preview.transform.position=(p+anchor.Value)*.5f+Vector3.up*(wallHeight*.5f+buildingLevel*LevelHeight);
     preview.transform.localScale=new Vector3(Mathf.Max(.05f,d.magnitude),wallHeight,wallThickness);
     preview.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg,0);
    }else{
     preview.transform.position=(p+anchor.Value)*.5f+Vector3.up*(FloorCenter+buildingLevel*LevelHeight+(currentTool==Tool.Roof?wallHeight:0f));
     preview.transform.localScale=new Vector3(Mathf.Max(gridSize,Mathf.Abs(d.x)),currentTool==Tool.Roof?.2f:FloorThickness,Mathf.Max(gridSize,Mathf.Abs(d.z)));
    }
   }else{
    preview.transform.position=p+Vector3.up*.04f;
    preview.transform.localScale=new Vector3(.25f,.08f,.25f);
   }
  }
  private Vector2 libraryScroll;
  private GUIStyle titleStyle,buttonStyle;
  void Notice(string message){status=message;statusUntil=Time.time+5f;}
  void SelectTool(Tool tool){
   bool changed=currentTool!=tool;
   currentTool=tool;
   anchor=null;
   dragAnchorArmed=false;
   draggingSelection=false;
   polygonCorners.Clear();
   if(changed){
    if(tool==Tool.Garage){openingWidth=3f;openingHeight=2.35f;openingBottom=0f;}
    else if(tool==Tool.Door){openingWidth=.95f;openingHeight=2.10f;openingBottom=0f;}
    else if(tool==Tool.Window){openingWidth=1.25f;openingHeight=1.15f;openingBottom=.95f;}
   }
   if(tool!=Tool.Imported&&importedGhost!=null)importedGhost.SetActive(false);
   if(preview!=null)preview.SetActive(false);
   if(openingGuide!=null)openingGuide.enabled=false;
   Notice(tool==Tool.Garage?"GARAGE opening selected":tool==Tool.Door?"DOOR opening selected":
    tool==Tool.Window?"WINDOW opening selected":tool+" selected");
  }
  string AssetLibrary(){string d=Path.Combine(Application.persistentDataPath,"ImportedAssets");Directory.CreateDirectory(d);return d;}
  void BrowseObj(){
   try{
    var psi=new System.Diagnostics.ProcessStartInfo();
    psi.FileName="powershell.exe";
    psi.Arguments="-NoProfile -STA -Command \"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='3D Models (*.obj;*.glb;*.gltf;*.zip)|*.obj;*.glb;*.gltf;*.zip|All Files (*.*)|*.*'; $d.CheckFileExists=$true; if($d.ShowDialog() -eq 'OK') { [Console]::Write($d.FileName) }\"";
    psi.UseShellExecute=false;
    psi.CreateNoWindow=true;
    psi.RedirectStandardOutput=true;
    using(var process=System.Diagnostics.Process.Start(psi)){
     string picked=process.StandardOutput.ReadToEnd();
     process.WaitForExit();
     if(!string.IsNullOrWhiteSpace(picked)){importPath=picked.Trim();importError="";}
    }
   }catch(Exception e){importError="Browse failed: "+e.Message;}
  }
  void ImportObj(){
   try{
    string path=importPath.Trim().Trim('"');
    if(!File.Exists(path))throw new FileNotFoundException("Choose an existing model file.");
    string[] categories={"All","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor",
     "Materials","Architecture","Other","Trees","Skyboxes"};
    string requested=catalogCategory>0&&catalogCategory<categories.Length&&
     catalogCategory!=8&&catalogCategory!=12?categories[catalogCategory]:null;
    string id=AssetHub.Import(path,"Other",requested);
    SelectImportedAsset(id);
    showImport=false;
    importError="";
    catalogCategory=0;showCatalog=true;
    Notice("Imported "+AssetHub.Name(id)+". Click the ground to place it.");
   }catch(Exception e){Debug.LogException(e);importError=e.Message;Notice("Import failed: "+e.Message);}
  }
  void ImportMaterialFile(){
   try{
    var psi=new System.Diagnostics.ProcessStartInfo();
    psi.FileName="powershell.exe";
    psi.Arguments="-NoProfile -STA -Command \"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Material package (*.zip;*.png;*.jpg;*.jpeg)|*.zip;*.png;*.jpg;*.jpeg'; $d.CheckFileExists=$true; if($d.ShowDialog() -eq 'OK') { [Console]::Write($d.FileName) }\"";
    psi.UseShellExecute=false;psi.CreateNoWindow=true;psi.RedirectStandardOutput=true;
    using(var process=System.Diagnostics.Process.Start(psi)){
     string chosen=process.StandardOutput.ReadToEnd().Trim();
     process.WaitForExit();
     if(string.IsNullOrEmpty(chosen))return;
     string folder=AssetHub.Downloads("Other");
     string target=Path.Combine(folder,Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(chosen));
     File.Copy(chosen,target);
     File.WriteAllText(target+".worldbuilder-category","Materials");
     MaterialLibrary.Scan();
     Notice("Material queued for discovery. Open Materials in a moment.");
    }
   }catch(Exception ex){Diagnostics.Add("ERROR","Material import failed: "+ex.Message);Notice("Material import failed: "+ex.Message);}
  }
  void CopyTree(string source,string destination){
   Directory.CreateDirectory(destination);
   foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);
   foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Path.Combine(destination,Path.GetFileName(dir)));
  }
  void CopyImportedAssets(string exportAssets){
   foreach(var item in project.pieces){
    if(string.IsNullOrEmpty(item.assetId))continue;
    string dir=Path.Combine(AssetLibrary(),item.assetId);
    if(!Directory.Exists(dir))throw new DirectoryNotFoundException("Missing asset: "+item.assetId);
    CopyTree(dir,Path.Combine(exportAssets,"ImportedAssets",item.assetId));
   }
  }
  void CopyPaintedMaterials(string exportAssets){
   var unique=new HashSet<string>();
   foreach(var piece in project.pieces){
    if(string.IsNullOrEmpty(piece.finish)||!piece.finish.StartsWith("texture:",StringComparison.Ordinal))continue;
    string id=piece.finish.Substring(8);
    if(!unique.Add(id))continue;
    string color=MaterialLibrary.ColorFile(id);
    if(color==null)throw new FileNotFoundException("Missing material texture for "+id);
    string directory=Path.Combine(exportAssets,"SurfaceMaterials",id);
    Directory.CreateDirectory(directory);
    File.Copy(color,Path.Combine(directory,Path.GetFileName(color)),true);
    foreach(string map in Directory.GetFiles(Path.Combine(MaterialLibrary.Root,id),"normal.*")){
     string ext=Path.GetExtension(map).ToLowerInvariant();
     if(ext==".png"||ext==".jpg"||ext==".jpeg")File.Copy(map,Path.Combine(directory,Path.GetFileName(map)),true);
    }
    foreach(string map in Directory.GetFiles(Path.Combine(MaterialLibrary.Root,id),"roughness.*")){
     string ext=Path.GetExtension(map).ToLowerInvariant();
     if(ext==".png"||ext==".jpg"||ext==".jpeg")File.Copy(map,Path.Combine(directory,Path.GetFileName(map)),true);
    }
   }
  }
  void ExportUnity(){
   try{
    if(project.pieces==null||project.pieces.Count==0){Notice("Build something before exporting");return;}
    string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"WorldBuilder-Exports");
    Directory.CreateDirectory(root);
    string destination=Path.Combine(root,"World-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    string assets=Path.Combine(destination,"Assets","WorldBuilderExport");
    Directory.CreateDirectory(assets);
    File.WriteAllText(Path.Combine(assets,"world.json"),JsonUtility.ToJson(project,true));
    CopyImportedAssets(assets);
    CopyPaintedMaterials(assets);
    var importer=Resources.Load<TextAsset>("WorldBuilderSceneImporter");
    if(importer==null)throw new InvalidDataException("Scene importer resource missing");
    File.WriteAllText(Path.Combine(assets,"WorldBuilderSceneImporter.cs"),importer.text);
    File.WriteAllText(Path.Combine(destination,"README.txt"),
      "WorldBuilder VRChat Export V1\\r\\n\\r\\n"+
      "This is an export package, not yet a complete Unity project or one-click VRChat upload.\\r\\n"+
      "1. Create/open a matching Unity project using VRChat Creator Companion (Worlds SDK).\\r\\n"+
      "2. Copy the CONTENTS of this package's Assets folder into that Unity project's Assets folder.\\r\\n"+
      "3. In Unity, select Tools > WorldBuilder > Import World. It will generate WorldBuilderWorld.unity automatically.\\r\\n"+
      "4. Review the spawn and any SDK warnings, then build and publish via VRChat SDK.\\r\\n"+
      "Export contains geometry, basic light settings, supported imported OBJ assets and texture maps. Review compatibility, baking and VRChat SDK interactions in Unity. Experimental features need testing.\\r\\n");
    string rootProject=targetVrcProject.Trim().Trim('"');
    if(rootProject.Length>0){
     string assetsTarget=Path.Combine(rootProject,"Assets");
     if(!Directory.Exists(assetsTarget)||!File.Exists(Path.Combine(rootProject,"ProjectSettings","ProjectVersion.txt")))
      throw new InvalidDataException("Choose an existing Unity project root folder");
     PlayerPrefs.SetString("WorldBuilder.VRChatProject",rootProject);
     PlayerPrefs.Save();
     string target=Path.Combine(assetsTarget,"WorldBuilderExport");
     CopyTree(assets,target);
     File.WriteAllText(Path.Combine(target,"WorldBuilderAutoImport.request"),"import");
     Notice("VRChat project prepared. Open Unity for automatic scene generation.");
    }else{
     Notice("Exported to Documents/WorldBuilder-Exports/"+Path.GetFileName(destination));
     Application.OpenURL(new Uri(destination).AbsoluteUri);
    }
    showExport=false;
   }catch(Exception e){Debug.LogException(e);Notice("Export failed: "+e.Message);}
  }
  void SelectSkybox(string id){
   if(!string.IsNullOrEmpty(id)&&SkyboxLibrary.ImagePath(id)==null){
    Notice("Skybox file is unavailable; see F2 Diagnostics");return;
   }
   if(project.skyboxId==id)return;
   RememberChange();
   project.skyboxId=id??"";
   ApplyEnvironment();
   Notice(string.IsNullOrEmpty(id)?"Gradient sky restored":"Skybox applied · Save your world to keep it");
  }
  void ImportSkyFile(){
   try{
    var psi=new System.Diagnostics.ProcessStartInfo();
    psi.FileName="powershell.exe";
    psi.Arguments="-NoProfile -STA -Command \"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Sky panoramas (*.hdr;*.jpg;*.jpeg;*.png)|*.hdr;*.jpg;*.jpeg;*.png'; $d.CheckFileExists=$true; if($d.ShowDialog() -eq 'OK') { [Console]::Write($d.FileName) }\"";
    psi.UseShellExecute=false;psi.CreateNoWindow=true;psi.RedirectStandardOutput=true;
    using(var process=System.Diagnostics.Process.Start(psi)){
     string path=process.StandardOutput.ReadToEnd().Trim();
     process.WaitForExit();
     if(string.IsNullOrEmpty(path))return;
     string id=SkyboxLibrary.Import(path,"Other");
     SelectSkybox(id);
    }
   }catch(Exception ex){Diagnostics.Add("ERROR","Skybox import failed: "+ex.Message);Notice("Could not import sky: "+ex.Message);}
  }
  void NewProject(){RememberChange();SelectObject(-1);ResetWallDefaults();project=new Project();foreach(var obj in objects)if(obj!=null)Destroy(obj);objects.Clear();anchor=null;Notice("New empty project. Save to keep it.");}
  string ProjectsFolder(){string p=Path.Combine(Application.persistentDataPath,"Projects");Directory.CreateDirectory(p);return p;}
  void SaveAsNamed(string name){
   try{string safe=string.Join("_",name.Split(Path.GetInvalidFileNameChars()));if(string.IsNullOrWhiteSpace(safe))safe="My World";
    filePath=Path.Combine(ProjectsFolder(),safe+".json");Save();}
   catch(Exception e){Notice("Save failed: "+e.Message);}
  }
  void OpenProject(string path){filePath=path;if(recoverySessionStarted)RecoveryStore.ChangeProject(filePath);Load();showProjects=false;}

  void Start() {
   if(sceneCamera==null)sceneCamera=Camera.main;
   if(sceneCamera!=null)walkthrough=sceneCamera.GetComponent<WalkthroughCamera>();
   targetVrcProject=PlayerPrefs.GetString("WorldBuilder.VRChatProject","");
   AssetHub.Initialize();
   // Asset websites now render through the embedded WebView2 browser.
   filePath=Path.Combine(Application.persistentDataPath,"WorldBuilder-project.json");
   try{
    interruptedRecovery=RecoveryStore.FindInterrupted();
    RecoveryStore.BeginSession(filePath);
    recoverySessionStarted=true;
   }catch(Exception ex){Diagnostics.Add("WARN","Recovery session could not start: "+ex.Message);}
   if(File.Exists(filePath))Load();
   showRecoveryPrompt=interruptedRecovery!=null;
   nextRecoveryTime=Time.unscaledTime+RecoveryInterval;
   ApplyEnvironment();
   BuildGrid();
  }
  Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x/gridSize)*gridSize,0,Mathf.Round(p.z/gridSize)*gridSize);
  bool MouseOnGround(out Vector3 point) {
   point=Vector3.zero;
   if(sceneCamera==null)return false;
   Ray ray=sceneCamera.ScreenPointToRay(Input.mousePosition);
   Plane ground=new Plane(Vector3.up,Vector3.zero);
   if(!ground.Raycast(ray,out float distance))return false;
   point=Snap(ray.GetPoint(distance));return true;
  }
  void BalanceSceneLights(){
   const float maxIntensity=1.6f,maxRange=12f;
   int count=0;
   foreach(var p in project.pieces)if((p.kind=="light-point"||p.kind=="light-spot")&&
    (p.lightIntensity>maxIntensity||p.lightRange>maxRange))count++;
   if(count==0){Notice("Scene lighting already balanced");return;}
   RememberChange();
   for(int i=0;i<project.pieces.Count;i++){
    Piece p=project.pieces[i];
    if(p.kind!="light-point"&&p.kind!="light-spot")continue;
    p.lightIntensity=Mathf.Min(p.lightIntensity,maxIntensity);
    p.lightRange=Mathf.Min(p.lightRange,maxRange);
    if(i<objects.Count&&objects[i]!=null){
     Light light=objects[i].GetComponent<Light>();
     if(light!=null){light.intensity=p.lightIntensity;light.range=p.lightRange;}
    }
   }
   Notice("Balanced "+count+" light"+(count==1?"":"s")+" · Undo to restore");
  }
  void ApplyEnvironment(){
   if(scenicBackdrop==null){
    var backdropObject=new GameObject("WorldBuilder Scenic Environment");
    scenicBackdrop=backdropObject.AddComponent<WorldBuilderBackdrop>();
    scenicBackdrop.Initialize();
   }
   if(sceneCamera!=null){
    sceneCamera.clearFlags=CameraClearFlags.SolidColor;
    Color[] palettes={new Color(.48f,.70f,.92f),new Color(.92f,.54f,.31f),new Color(.045f,.064f,.14f),new Color(.7f,.77f,.81f)};
    int sky=Mathf.Clamp(project.skyPreset,0,palettes.Length-1);
    sceneCamera.backgroundColor=palettes[sky];
    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight=sky==2?new Color(.07f,.10f,.18f):
     sky==1?new Color(.32f,.28f,.27f):new Color(.34f,.38f,.44f);
    RenderSettings.fog=project.fog;
    RenderSettings.fogColor=palettes[sky];
    RenderSettings.fogDensity=.009f;
   }
   if(editorSun==null){
    var sun=new GameObject("WorldBuilder Studio Sun");
    editorSun=sun.AddComponent<Light>();
    editorSun.type=LightType.Directional;
   }
   editorSun.intensity=project.skyPreset==2?.025f:project.skyPreset==1?.42f:project.skyPreset==3?.43f:.70f;
   editorSun.transform.rotation=Quaternion.Euler(project.sunPitch,project.sunYaw,0);
   if(scenicBackdrop!=null)scenicBackdrop.ApplySceneMood(project.skyPreset,sceneCamera,project.skyboxId);
  }
  void CompletePolygonRoom(){
   if(polygonCorners.Count<3){Notice("Click at least three corners");return;}
   Vector3 middle=Vector3.zero;
   foreach(var p in polygonCorners)middle+=p;
   middle/=polygonCorners.Count;
   var local=new List<Vector3>();
   foreach(var p in polygonCorners)local.Add(new Vector3(p.x-middle.x,0,p.z-middle.z));
   try{
    Mesh validation=PolygonGeometry.Create(local,FloorThickness);
    Destroy(validation);
    RememberChange();
    string group=Guid.NewGuid().ToString("N");
    for(int i=0;i<polygonCorners.Count;i++){
     var wall=WallBetween(polygonCorners[i],polygonCorners[(i+1)%polygonCorners.Count]);
     wall.roomId=group;AddWithoutHistory(wall);
    }
    var floor=new Piece("polygon-floor",new Vector3(middle.x,FloorCenter+buildingLevel*LevelHeight,middle.z),Vector3.one,Quaternion.identity);
    floor.vertices=local;floor.roomId=group;AddWithoutHistory(floor);
    polygonCorners.Clear();
    Notice("Custom room created with "+local.Count+" corners");
   }catch(Exception ex){Diagnostics.Add("WARN","Invalid room outline: "+ex.Message);Notice("Room outline invalid: "+ex.Message);}
  }
  void SyncWaterBasins(){
   if(project==null||scenicBackdrop==null)return;
   unchecked{
    int hash=17;
    foreach(Piece p in project.pieces){
     if(p.kind!="water"&&p.kind!="terrain")continue;
     hash=hash*31+p.kind.GetHashCode();
     hash=hash*31+p.position.GetHashCode();
     hash=hash*31+p.scale.GetHashCode();
    }
    if(hash==basinFingerprint)return;
    basinFingerprint=hash;
   }
   scenicBackdrop.SetWaterAreas(project.pieces);
   for(int i=0;i<project.pieces.Count&&i<objects.Count;i++){
    Piece p=project.pieces[i];
    if(p.kind!="terrain"||objects[i]==null)continue;
    var filter=objects[i].GetComponent<MeshFilter>();
    var collider=objects[i].GetComponent<MeshCollider>();
    if(filter==null)continue;
    Mesh fresh=TerrainGeometry.Create(32,Mathf.Max(2,p.scale.x),
     Mathf.Max(2,p.scale.z),p.scale.y,
     Mathf.RoundToInt(p.position.x*13f+p.position.z*17f));
    BasinGeometry.CarveTerrain(fresh,p,project.pieces);
    Mesh old=filter.sharedMesh;
    filter.sharedMesh=fresh;
    if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=fresh;}
    if(old!=null)Destroy(old);
   }
  }
  void Update() {
   if(showRecoveryPrompt)return;
   SyncWaterBasins();
   if(Time.unscaledTime>=nextRecoveryTime){
    nextRecoveryTime=Time.unscaledTime+RecoveryInterval;
    WriteRecovery();
   }
   if(walkthrough!=null&&walkthrough.IsWalking)return;
   if(Input.GetKeyDown(KeyCode.F2)){showDiagnostics=!showDiagnostics;showPause=false;showHelp=false;showSettings=false;}
   if(showAssets&&(browserProcess==null||browserProcess.HasExited)){showAssets=false;if(browserProcess!=null){browserProcess.Dispose();browserProcess=null;}}
   // No filesystem scans, ZIP extraction, mesh imports, or thumbnail
   // generation in Update. Large asset downloads previously blocked the
   // Unity main thread and Windows reported "Stopped responding".
   // Users explicitly start a limited import from My Assets instead.
   if(Input.GetKeyDown(KeyCode.Alpha0)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Select);
   if(Input.GetKeyDown(KeyCode.T)&&!showPause&&!showSettings&&!showHelp)SetCameraView(true);
   if(Input.GetKeyDown(KeyCode.P)&&!showPause&&!showSettings&&!showHelp)SetCameraView(false);
   if(Input.GetKeyDown(KeyCode.Alpha1)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Wall);
   if(Input.GetKeyDown(KeyCode.Alpha2)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Floor);
   if(Input.GetKeyDown(KeyCode.Alpha3)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Couch);
   if(Input.GetKeyDown(KeyCode.Alpha4)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Table);
   if(Input.GetKeyDown(KeyCode.Alpha5)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Erase);
   if(Input.GetKeyDown(KeyCode.Alpha6)&&!showPause&&!showSettings&&!showHelp)SelectTool(Tool.Room);
   if(Input.GetKeyDown(KeyCode.M)&&!showPause&&!showSettings&&!showHelp){catalogCategory=8;showCatalog=true;SelectTool(Tool.Paint);}
   if(Input.GetKeyDown(KeyCode.Escape)){
    if(currentTool==Tool.Room&&polygonMode&&polygonCorners.Count>0){polygonCorners.Clear();Notice("Custom room outline cancelled");return;}
    if(showStudio){showStudio=false;return;}
    if(showDiagnostics){showDiagnostics=false;return;}
    if(showConfirmExit){showConfirmExit=false;showPause=true;}
    else if(showImport||showExport||showProjects||showSettings||showHelp){
     showImport=false;showExport=false;showProjects=false;showSettings=false;showHelp=false;
    }else if(currentTool!=Tool.Select||anchor.HasValue){
     SelectTool(Tool.Select);
     Notice("Placement cancelled · Select tool active");
     return;
    }else{showPause=!showPause;anchor=null;}
   }
   if(Input.GetKeyDown(KeyCode.F1)){showHelp=!showHelp;showPause=false;showConfirmExit=false;}
   if(showDiagnostics||showPause||showConfirmExit||showSettings||showHelp||showImport||showExport||showProjects){
    if(importedGhost!=null)importedGhost.SetActive(false);
    if(preview!=null)preview.SetActive(false);
    return;
   }
   if(Input.GetKeyDown(KeyCode.G)&&!Input.GetKey(KeyCode.LeftControl)&&!Input.GetKey(KeyCode.RightControl))ToggleGrid();
   if(currentTool==Tool.Room&&Input.GetKeyDown(KeyCode.C)){polygonMode=!polygonMode;polygonCorners.Clear();anchor=null;dragAnchorArmed=false;Notice(polygonMode?"Custom room: click corners, Enter to finish":"Rectangular room mode");}
   if(currentTool==Tool.Room&&polygonMode){
    if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))CompletePolygonRoom();
    if(Input.GetKeyDown(KeyCode.Backspace)&&polygonCorners.Count>0)polygonCorners.RemoveAt(polygonCorners.Count-1);
   }
   if(Input.GetKeyDown(KeyCode.N)){furnitureSnap=!furnitureSnap;Notice("Furniture snap "+(furnitureSnap?"on":"off"));}
   if(!showPause&&!showConfirmExit&&!showSettings&&!showExport&&!showImport&&!showProjects){
    if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.G)){
     if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))UngroupSelected();else GroupSelected();
    }
    if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.D))DuplicateSelection();
    if(Input.GetKeyDown(KeyCode.R)&&currentTool==Tool.Select)RotateSelection();
    else if(currentTool==Tool.Imported&&(Input.GetKeyDown(KeyCode.R)||Input.GetKeyDown(KeyCode.E))){importedRotation=(importedRotation+45f)%360f;Notice("Asset rotation: "+importedRotation.ToString("F0")+"°");}
    if(currentTool==Tool.Imported&&Input.GetKeyDown(KeyCode.Q)){importedRotation=(importedRotation+315f)%360f;Notice("Asset rotation: "+importedRotation.ToString("F0")+"°");}
    if(Input.GetKeyDown(KeyCode.Delete)&&currentTool==Tool.Select)DeleteSelection();
    if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Y))RedoLast();
   }
   if(draggingSelection){
    if(Input.GetMouseButtonUp(0)){draggingSelection=false;Notice("Object moved");}
    else if(selectedIndex>=0&&selectedIndex<project.pieces.Count&&!OverUI()&&MouseOnGround(out Vector3 dragPoint)){
     var piece=project.pieces[selectedIndex];
     Vector3 target=dragPoint+selectionDragOffset;
     target.y=piece.position.y;
     Vector3 movement=target-piece.position;
     if(movement.sqrMagnitude>.000001f){
      if(multiSelection.Count>1&&!IsRoomFloor(selectedIndex)){
       foreach(int i in multiSelection){
        if(i<0||i>=project.pieces.Count||objects[i]==null)continue;
        project.pieces[i].position+=movement;
        objects[i].transform.position=project.pieces[i].position;
       }
      }else if(IsRoomFloor(selectedIndex)){
       string group=piece.roomId;
       for(int j=0;j<project.pieces.Count;j++){
        if(project.pieces[j].roomId!=group||objects[j]==null)continue;
        project.pieces[j].position+=movement;
        objects[j].transform.position=project.pieces[j].position;
       }
      }else{
       piece.position=target;
       objects[selectedIndex].transform.position=target;
      }
      RefreshSelectionOutline();
     }
    }
   }
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))UndoLast();
   if(dragAnchorArmed&&Input.GetMouseButtonUp(0)){
    dragAnchorArmed=false;
    if(anchor.HasValue&&!OverUI()&&
      (new Vector2(Input.mousePosition.x,Input.mousePosition.y)-dragAnchorMouse).sqrMagnitude>=196f&&
      MouseOnGround(out Vector3 drop)&&Vector3.Distance(anchor.Value,drop)>.01f){
     Vector3 dragStart=anchor.Value;
     anchor=null;
     CompleteSpan(dragStart,drop);
    }
   }
   UpdatePreview();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.S))Save();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.O))Load();
   if(!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftAlt) || Input.GetMouseButton(1))return;
   if(OverUI())return;
   bool clickedGround=MouseOnGround(out Vector3 p);
   if(!clickedGround&&currentTool!=Tool.Door&&currentTool!=Tool.Window&&currentTool!=Tool.Garage&&currentTool!=Tool.Paint)return;
   if(currentTool==Tool.Select){
    Ray pick=sceneCamera.ScreenPointToRay(Input.mousePosition);
    if(Physics.Raycast(pick,out RaycastHit hit,500f)){
     int hitIndex=-1;
     for(int j=0;j<objects.Count;j++){
      if(objects[j]!=null&&(objects[j]==hit.collider.gameObject||hit.collider.transform.IsChildOf(objects[j].transform))){hitIndex=j;break;}
     }
     if((Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))&&hitIndex>=0)ToggleMultiple(hitIndex);
     else SelectObject(hitIndex);
     if(hitIndex>=0&&!Input.GetKey(KeyCode.LeftShift)&&!Input.GetKey(KeyCode.RightShift)){
      RememberChange();
      draggingSelection=true;
      selectionDragOffset=project.pieces[hitIndex].position-p;
     }
    }else SelectObject(-1);
    return;
   }
   if(currentTool==Tool.Erase) { Erase(p);return; }
   if(currentTool==Tool.Paint){PaintSurface();return;}
   if(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage){AddOpening(currentTool==Tool.Window);return;}
   if(currentTool==Tool.Room&&polygonMode){polygonCorners.Add(p);Notice("Corner "+polygonCorners.Count+" · Enter to close room");return;}
   if(currentTool==Tool.Light){var piece=new Piece(lightMode==0?"light-point":"light-spot",p+Vector3.up*2.35f,Vector3.one,Quaternion.Euler(90,0,0));piece.lightIntensity=lightBrightness;piece.lightRange=lightRadius;Add(piece);return;}
   if(currentTool==Tool.Tree){Add(new Piece("tree",p,new Vector3(1f,4f,1f),Quaternion.identity));return;}
   if(currentTool==Tool.Mirror){Add(new Piece("vrc-mirror",p+Vector3.up*1.4f,new Vector3(1.5f,2f,.05f),Quaternion.identity));return;}
   if(currentTool==Tool.Seat){Add(new Piece("vrc-seat",p+Vector3.up*.5f,new Vector3(.55f,.2f,.55f),Quaternion.identity));return;}
   if(currentTool==Tool.Spawn){Add(new Piece("vrc-spawn",p+Vector3.up*1f,new Vector3(.3f,.3f,.3f),Quaternion.identity));return;}
   if(currentTool==Tool.Imported){
    if(string.IsNullOrEmpty(importedId)){Notice("Choose an imported model first");return;}
    if(!importedBoundsReady){Notice("This model could not be loaded; see F2 Diagnostics");return;}
    var item=new Piece("imported",SnapFurniture(ImportedPlacement(p)),Vector3.one*importedScale,Quaternion.Euler(0,importedRotation,0));
    item.assetId=importedId;
    item.fitToSurface=true;
    Add(item);return;
   }
   if(currentTool==Tool.Couch||currentTool==Tool.Table) {
    Add(new Piece(currentTool==Tool.Couch?"couch":"table",p+Vector3.up*.4f,new Vector3(currentTool==Tool.Couch?2f:1.2f,.8f,currentTool==Tool.Couch?.9f:1.2f),Quaternion.identity));return;
   }
   if(!anchor.HasValue){
    anchor=p;
    dragAnchorArmed=IsSpanTool(currentTool);
    dragAnchorMouse=new Vector2(Input.mousePosition.x,Input.mousePosition.y);
    return;
   }
   Vector3 start=anchor.Value;
   anchor=null;dragAnchorArmed=false;
   CompleteSpan(start,p);
  }
  // Allows click-once + click-again and press-drag-release with identical geometry.
  public static bool IsSpanTool(Tool tool){
   return tool==Tool.Wall||tool==Tool.Floor||tool==Tool.Room||
    tool==Tool.Roof||tool==Tool.Stairs||tool==Tool.Terrain||
    tool==Tool.Water||tool==Tool.Path;
  }
  void CompleteSpan(Vector3 start,Vector3 p){
   if(Vector3.Distance(start,p)<.01f)return;
   if(currentTool==Tool.Room){MakeRoom(start,p);return;}
   if(currentTool==Tool.Terrain){Add(new Piece("terrain",(start+p)*.5f,new Vector3(Mathf.Max(2f,Mathf.Abs(start.x-p.x)),terrainAmplitude,Mathf.Max(2f,Mathf.Abs(start.z-p.z))),Quaternion.identity));return;}
   if(currentTool==Tool.Path){Add(new Piece("path",(start+p)*.5f+Vector3.up*.04f,new Vector3(Mathf.Max(.5f,Mathf.Abs(start.x-p.x)),.08f,Mathf.Max(.5f,Mathf.Abs(start.z-p.z))),Quaternion.identity));return;}
   if(currentTool==Tool.Water){
    // Water is a volume with its top at the design ground height. A depth
    // control is necessary for pools/beaches even before terrain excavation.
    float depth=Mathf.Clamp(newWaterDepth,.15f,12f);
    Add(new Piece("water",(start+p)*.5f+Vector3.down*(depth*.5f),
     new Vector3(Mathf.Max(1f,Mathf.Abs(start.x-p.x)),depth,
      Mathf.Max(1f,Mathf.Abs(start.z-p.z))),Quaternion.identity));
    return;
   }
   if(currentTool==Tool.Roof){MakeRoof(start,p);return;}
   if(currentTool==Tool.Stairs){MakeStairs(start,p);return;}
   if(currentTool==Tool.Wall){
    start=SnapToExistingEnd(start);p=SnapToExistingEnd(p);
    if(Vector3.Distance(start,p)>.01f)Add(WallBetween(start,p));
   }else{
    Vector3 delta=p-start;
    Add(new Piece("floor",(start+p)*.5f+Vector3.up*(FloorCenter+buildingLevel*LevelHeight),new Vector3(Mathf.Max(Mathf.Abs(delta.x),gridSize),FloorThickness,Mathf.Max(Mathf.Abs(delta.z),gridSize)),Quaternion.identity));
   }
  }
  void Add(Piece piece){RememberChange();AddWithoutHistory(piece);}
  void AddWithoutHistory(Piece piece){
   // Do not persist a new piece unless its geometry/material was created.
   GameObject created=Create(piece);
   SelectObject(-1);
   project.pieces.Add(piece);
   objects.Add(created);
  }
  Vector3 SnapToExistingEnd(Vector3 input){
   float radius=Mathf.Max(.25f,gridSize*.55f);
   Vector3 snapped=input;float best=radius;
   foreach(var wall in project.pieces){
    if(wall.kind!="wall")continue;
    Vector3 dir=wall.rotation*Vector3.right*wall.scale.x*.5f;
    Vector3 a=wall.position-dir,b=wall.position+dir;
    a.y=0;b.y=0;
    float da=Vector3.Distance(input,a),db=Vector3.Distance(input,b);
    if(da<best){best=da;snapped=a;}
    if(db<best){best=db;snapped=b;}
   }
   return snapped;
  }
  Piece WallBetween(Vector3 a,Vector3 b){
   Vector3 d=b-a;
   return new Piece("wall",(a+b)*.5f+Vector3.up*(wallHeight*.5f+buildingLevel*LevelHeight),
     new Vector3(d.magnitude,wallHeight,wallThickness),
     Quaternion.Euler(0,-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg,0));
  }
  void MakeRoom(Vector3 a,Vector3 b){
   float x1=Mathf.Min(a.x,b.x),x2=Mathf.Max(a.x,b.x);
   float z1=Mathf.Min(a.z,b.z),z2=Mathf.Max(a.z,b.z);
   if(x2-x1<gridSize*.5f||z2-z1<gridSize*.5f){Notice("Room must have width and depth");return;}
   RememberChange();
   string id=Guid.NewGuid().ToString("N");
   var p1=new Vector3(x1,0,z1);var p2=new Vector3(x2,0,z1);
   var p3=new Vector3(x2,0,z2);var p4=new Vector3(x1,0,z2);
   Piece[] walls={WallBetween(p1,p2),WallBetween(p2,p3),WallBetween(p3,p4),WallBetween(p4,p1)};
   foreach(var wall in walls){wall.roomId=id;AddWithoutHistory(wall);}
   var floor=new Piece("floor",new Vector3((x1+x2)*.5f,FloorCenter+buildingLevel*LevelHeight,(z1+z2)*.5f),
     new Vector3(x2-x1,FloorThickness,z2-z1),Quaternion.identity);
   floor.roomId=id;
   AddWithoutHistory(floor);
   Notice("Room created: "+(x2-x1).ToString("F1")+" × "+(z2-z1).ToString("F1")+" m");
  }
  bool IsRoomFloor(int index){
   return index>=0&&index<project.pieces.Count&&project.pieces[index].kind=="floor"&&
    !string.IsNullOrEmpty(project.pieces[index].roomId);
  }
  void ResizeRoom(int index,Vector3 newCenter,float newWidth,float newDepth){
   if(!IsRoomFloor(index))return;
   newWidth=Mathf.Max(.5f,newWidth);newDepth=Mathf.Max(.5f,newDepth);
   Piece floor=project.pieces[index];
   string id=floor.roomId;
   float level=(floor.position.y-FloorCenter)/LevelHeight;
   int oldLevel=buildingLevel;
   // Reconstruct grouped walls at the same elevation; openings must be
   // placed again because resizing a wall changes their offset/size.
   buildingLevel=Mathf.Clamp(Mathf.RoundToInt(level),0,8);
   RememberChange();
   for(int k=project.pieces.Count-1;k>=0;k--){
    if(project.pieces[k].roomId!=id)continue;
    if(objects[k]!=null)Destroy(objects[k]);
    objects.RemoveAt(k);project.pieces.RemoveAt(k);
   }
   SelectObject(-1);
   Vector3 p1=new Vector3(newCenter.x-newWidth*.5f,0,newCenter.z-newDepth*.5f);
   Vector3 p2=p1+Vector3.right*newWidth;
   Vector3 p3=p2+Vector3.forward*newDepth;
   Vector3 p4=p1+Vector3.forward*newDepth;
   foreach(var wall in new[]{WallBetween(p1,p2),WallBetween(p2,p3),WallBetween(p3,p4),WallBetween(p4,p1)}){
    wall.roomId=id;AddWithoutHistory(wall);
   }
   floor.position=new Vector3(newCenter.x,FloorCenter+buildingLevel*LevelHeight,newCenter.z);
   floor.scale=new Vector3(newWidth,FloorThickness,newDepth);
   AddWithoutHistory(floor);
   buildingLevel=oldLevel;
   Notice("Room resized to "+newWidth.ToString("F2")+" × "+newDepth.ToString("F2")+" m");
  }
  void MakeRoof(Vector3 a,Vector3 b){
   float width=Mathf.Abs(b.x-a.x),depth=Mathf.Abs(b.z-a.z);
   if(width<.25f||depth<.25f){Notice("Roof needs width and depth");return;}
   float cx=(a.x+b.x)*.5f,cz=(a.z+b.z)*.5f;
   // Seat the roof's underside directly on the actual top of nearby walls,
   // including walls that use a custom height instead of the level preset.
   float desiredBase=buildingLevel*LevelHeight+wallHeight;
   float closest=float.MaxValue;
   for(int i=0;i<project.pieces.Count;i++){
    Piece wall=project.pieces[i];
    if(wall.kind!="wall")continue;
    float dx=Mathf.Abs(wall.position.x-cx),dz=Mathf.Abs(wall.position.z-cz);
    if(dx>width*.5f+wall.scale.x*.5f+.3f||dz>depth*.5f+wall.scale.x*.5f+.3f)continue;
    float wallBase=wall.position.y-wall.scale.y*.5f;
    float levelBase=buildingLevel*LevelHeight;
    if(Mathf.Abs(wallBase-levelBase)>1f)continue;
    float top=wall.position.y+wall.scale.y*.5f;
    float distance=Vector2.Distance(new Vector2(cx,cz),new Vector2(wall.position.x,wall.position.z));
    if(distance<closest){closest=distance;desiredBase=top;}
   }
   float thickness=.20f;
   Add(new Piece("roof",new Vector3(cx,desiredBase+thickness*.5f,cz),
      new Vector3(width,thickness,depth),Quaternion.identity));
   Notice("Roof aligned to wall top at "+desiredBase.ToString("F2")+" m");
  }
  void MakeStairs(Vector3 a,Vector3 b){
   Vector3 delta=b-a;delta.y=0;
   float run=delta.magnitude;
   if(run<1.5f){Notice("Stairs need at least 1.5 meters of run");return;}
   int steps=Mathf.Clamp(Mathf.CeilToInt(LevelHeight/.18f),8,24);
   float stepRun=run/steps;
   Vector3 direction=delta.normalized;
   float angle=-Mathf.Atan2(direction.z,direction.x)*Mathf.Rad2Deg;
   RememberChange();
   string group=Guid.NewGuid().ToString("N");
   for(int k=0;k<steps;k++){
    float rise=LevelHeight*(k+1)/steps;
    Vector3 center=a+direction*((k+.5f)*stepRun)+Vector3.up*(buildingLevel*LevelHeight+rise*.5f);
    var step=new Piece("stairs",center,new Vector3(stepRun+.02f,rise,1.2f),
     Quaternion.Euler(0,angle,0));
    step.groupId=group;
    AddWithoutHistory(step);
   }
   // Simple wood railing and evenly spaced balusters, exported as ordinary geometry.
   Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
   Vector3 slope=delta+Vector3.up*LevelHeight;
   Quaternion railRotation=Quaternion.FromToRotation(Vector3.right,slope.normalized);
   for(int sign=-1;sign<=1;sign+=2){
    Vector3 railCenter=(a+b)*.5f+Vector3.up*(buildingLevel*LevelHeight+LevelHeight*.5f+1.0f)+side*(sign*.66f);
    var railing=new Piece("stair-rail",railCenter,new Vector3(slope.magnitude,.09f,.09f),railRotation);
    railing.groupId=group;AddWithoutHistory(railing);
    for(int j=0;j<5;j++){
     float t=j/4f;
     Vector3 position=Vector3.Lerp(a,b,t)+Vector3.up*(buildingLevel*LevelHeight+LevelHeight*t+.52f)+side*(sign*.66f);
     var post=new Piece("stair-post",position,new Vector3(.075f,1.02f,.075f),Quaternion.identity);
     post.groupId=group;AddWithoutHistory(post);
    }
   }
   Notice("Staircase created with railings: "+steps+" steps");
  }
  bool FindOpeningWall(out int index,out Vector3 hitPoint){
   index=-1;hitPoint=Vector3.zero;
   if(sceneCamera==null)return false;
   Ray ray=sceneCamera.ScreenPointToRay(Input.mousePosition);
   RaycastHit[] hits=Physics.RaycastAll(ray,300f);
   Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   foreach(var hit in hits){
    for(int i=0;i<objects.Count;i++){
     if(project.pieces[i].kind!="wall"||objects[i]==null)continue;
     if(hit.collider.gameObject!=objects[i]&&!hit.collider.transform.IsChildOf(objects[i].transform))continue;
     if(project.pieces[i].scale.x<1.15f||project.pieces[i].scale.y<1.6f)continue;
     index=i;hitPoint=hit.point;return true;
    }
   }
   return false;
  }
  void PaintSurface(){
   if(sceneCamera==null)return;
   Ray ray=sceneCamera.ScreenPointToRay(Input.mousePosition);
   if(!Physics.Raycast(ray,out RaycastHit hit,500f)){Notice("Aim at a wall, floor or roof");return;}
   int index=-1;
   for(int i=0;i<objects.Count;i++){
    if(objects[i]==null)continue;
    if(hit.collider.gameObject==objects[i]||hit.collider.transform.IsChildOf(objects[i].transform)){index=i;break;}
   }
   if(index<0){Notice("Choose a WorldBuilder object or surface to paint");return;}
   Piece p=project.pieces[index];
   if(p.kind!="wall"&&p.kind!="floor"&&p.kind!="roof"&&p.kind!="imported"&&
      p.kind!="water"&&p.kind!="terrain"&&p.kind!="path"){
    Notice("Select a model, wall, floor, roof or landscape surface to paint");return;
   }
   string desired=string.IsNullOrEmpty(selectedTextureId)?FinishNames[selectedFinish]:"texture:"+selectedTextureId;
   if(p.finish==desired&&Mathf.Abs(p.textureTile-selectedTextureTile)<.001f)return;
   RememberChange();
   p.finish=desired;
   p.textureTile=selectedTextureTile;
   if(objects[index]!=null)Destroy(objects[index]);
   objects[index]=Create(p);
   if(index==selectedIndex)RefreshSelectionOutline();
   Notice("Applied "+p.finish);
  }
  void AddOpening(bool window){
   if(!FindOpeningWall(out int idx,out Vector3 wallPoint)){Notice("Aim at a wall to place an opening");return;}
   Piece original=project.pieces[idx];
   float length=original.scale.x,height=original.scale.y;
   float width=openingWidth;
   float openingHeight=Mathf.Min(this.openingHeight,height-.15f);
   if(length<width+.18f||height<openingHeight+.10f||openingHeight<.5f){
    Notice("Need a longer / taller wall for the "+currentTool+" opening");return;
   }
   Vector3 local=Quaternion.Inverse(original.rotation)*(wallPoint-original.position);
   float center=Mathf.Clamp(local.x,-length*.5f+width*.5f+.08f,length*.5f-width*.5f-.08f);
   float left=center-width*.5f,right=center+width*.5f;
   float bottom=Mathf.Clamp(openingBottom,0,height-openingHeight);
   RememberChange();
   if(objects[idx]!=null)Destroy(objects[idx]);
   objects.RemoveAt(idx);project.pieces.RemoveAt(idx);
   float t=original.scale.z;
   Action<float,float,float,float,string> part=(xStart,xEnd,yStart,yEnd,kind)=>{
    if(xEnd-xStart<.025f||yEnd-yStart<.025f)return;
    Vector3 localPos=new Vector3((xStart+xEnd)*.5f,(yStart+yEnd)*.5f-height*.5f,0);
    Piece section=new Piece(kind,original.position+original.rotation*localPos,
      new Vector3(xEnd-xStart,yEnd-yStart,t),original.rotation);
    section.roomId=original.roomId;
    section.finish=original.finish;
    section.textureTile=original.textureTile;
    AddWithoutHistory(section);
   };
   part(-length*.5f,left,0,height,"wall");
   part(right,length*.5f,0,height,"wall");
   part(left,right,bottom+openingHeight,height,"wall");
   if(window)part(left,right,0,bottom,"wall");
   // Thin contrasting trim remains visible once the preview disappears.
   // It sits at the perimeter, leaving the doorway/window opening unobstructed.
   float rim=.055f,frameDepth=Mathf.Max(.10f,t+.05f);
   Action<float,float,float,float> trim=(fromX,toX,fromY,toY)=>{
    if(toX<=fromX||toY<=fromY)return;
    Vector3 localPos=new Vector3((fromX+toX)*.5f,(fromY+toY)*.5f-height*.5f,0f);
    Piece frame=new Piece("opening-frame",original.position+original.rotation*localPos,
      new Vector3(toX-fromX,toY-fromY,frameDepth),original.rotation);
    frame.roomId=original.roomId;
    frame.finish=original.finish;
    frame.textureTile=original.textureTile;
    AddWithoutHistory(frame);
   };
   if(currentTool!=Tool.Garage){
    trim(left-rim,left,bottom,bottom+openingHeight);
    trim(right,right+rim,bottom,bottom+openingHeight);
    trim(left-rim,right+rim,bottom+openingHeight,bottom+openingHeight+rim);
    if(window)trim(left-rim,right+rim,bottom-rim,bottom);
   }
   // The gap remains an actual opening with colliders on surrounding pieces.
   Notice(currentTool==Tool.Garage?"Garage opening created":window?"Window opening created":"Door opening created");
  }
  Texture2D GetMaterialImage(string id){
   if(materialImages.TryGetValue(id,out Texture2D tex))return tex;
   string file=MaterialLibrary.ColorFile(id);
   if(file==null)return null;
   try{
    tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!tex.LoadImage(File.ReadAllBytes(file))){Destroy(tex);return null;}
    tex.wrapMode=TextureWrapMode.Repeat;
    materialImages[id]=tex;
    return tex;
   }catch(Exception ex){Diagnostics.Add("ERROR","Material image unreadable: "+ex.Message);return null;}
  }
  Texture2D GetAssociatedTexture(string assetId,string filename){
   string path=AssetHub.ModelTexturePath(assetId,filename);
   if(path==null)return null;
   if(associatedImages.TryGetValue(path,out Texture2D cached))return cached;
   try{
    var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!image.LoadImage(File.ReadAllBytes(path))){Destroy(image);return null;}
    image.wrapMode=TextureWrapMode.Repeat;
    associatedImages[path]=image;
    return image;
   }catch(Exception ex){Diagnostics.Add("WARN","Model texture failed to open: "+ex.Message);return null;}
  }
  void SetImportedFinish(Piece piece,string finish){
   if(piece==null||piece.kind!="imported"||piece.finish==finish)return;
   int index=selectedIndex;
   if(index<0||index>=objects.Count)return;
   RememberChange();
   piece.finish=finish;
   if(objects[index]!=null)Destroy(objects[index]);
   objects[index]=Create(piece);
   RefreshSelectionOutline();
   Notice(finish==""?"Restored original model materials":"Applied model-specific texture");
  }
  // Imported OBJ/GLB materials live on child mesh renderers, not on the
  // parent GameObject. Preserve each material by default and replace it only
  // when the user explicitly paints the asset.
  void ApplyImportedFinish(GameObject root,Piece piece){
   if(string.IsNullOrEmpty(piece.finish)||piece.finish=="Default")return;
   Texture2D image=null;Color color=Color.white;
   if(piece.finish.StartsWith("texture:",StringComparison.Ordinal)){
    image=GetMaterialImage(piece.finish.Substring(8));
    if(image==null){Diagnostics.Add("WARN","Missing paint texture on imported model: "+piece.finish);return;}
   }else if(piece.finish.StartsWith("assettexture:",StringComparison.Ordinal)){
    image=GetAssociatedTexture(piece.assetId,piece.finish.Substring("assettexture:".Length));
    if(image==null){Diagnostics.Add("WARN","Model's own texture is missing: "+piece.finish);return;}
   }else{
    int index=Array.IndexOf(FinishNames,piece.finish);
    if(index<0)return;
    color=FinishColors[index];
   }
   foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true)){
    var original=renderer.sharedMaterials;
    for(int i=0;i<original.Length;i++){
     var finish=WorldBuilderShaders.NewLit(color);
     if(image!=null){
      finish.mainTexture=image;
      float repeat=Mathf.Clamp(piece.textureTile<=0?1f:piece.textureTile,.1f,20f);
      finish.mainTextureScale=new Vector2(repeat,repeat);
     }
     original[i]=finish;
    }
    renderer.sharedMaterials=original;
   }
  }
  GameObject Create(Piece piece){
   if(piece.kind=="tree"){
    GameObject root=new GameObject("WB_Tree");
    root.transform.SetPositionAndRotation(piece.position,piece.rotation);
    Color wood=new Color(.35f,.26f,.17f),leaf=new Color(.28f,.47f,.24f);
    GameObject trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name="Natural Trunk";trunk.transform.SetParent(root.transform,false);
    trunk.transform.localPosition=new Vector3(0,1.06f,0);
    trunk.transform.localScale=new Vector3(.30f,1.06f,.30f);
    trunk.GetComponent<Renderer>().sharedMaterial=WorldBuilderShaders.NewLit(wood);
    Vector3[] centers={
     new Vector3(0f,3.02f,0f),new Vector3(-.82f,2.56f,.17f),
     new Vector3(.79f,2.62f,-.1f),new Vector3(.19f,3.59f,.15f),
     new Vector3(-.24f,2.83f,-.66f)};
    Vector3[] sizes={
     new Vector3(1.85f,1.75f,1.6f),new Vector3(1.45f,1.25f,1.42f),
     new Vector3(1.42f,1.35f,1.28f),new Vector3(1.3f,1.18f,1.2f),
     new Vector3(1.24f,1.34f,1.38f)};
    for(int n=0;n<centers.Length;n++){
     GameObject crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);
     crown.name="Leaf Cluster "+(n+1);crown.transform.SetParent(root.transform,false);
     crown.transform.localPosition=centers[n];crown.transform.localScale=sizes[n];
     crown.GetComponent<Renderer>().sharedMaterial=WorldBuilderShaders.NewLit(leaf*
      (n%2==0?1.06f:.88f));
    }
    root.transform.localScale=new Vector3(piece.scale.x,piece.scale.y/4f,piece.scale.z);
    return root;
   }

   if(piece.kind=="polygon-floor"||piece.kind=="terrain"){
    GameObject meshObj=new GameObject("WB_"+piece.kind);
    Mesh mesh=piece.kind=="polygon-floor"?PolygonGeometry.Create(piece.vertices,FloorThickness):
      TerrainGeometry.Create(32,Mathf.Max(2,piece.scale.x),Mathf.Max(2,piece.scale.z),piece.scale.y,
       Mathf.RoundToInt(piece.position.x*13f+piece.position.z*17f));
    var filter=meshObj.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
    var renderer=meshObj.AddComponent<MeshRenderer>();
    Shader shader=WorldBuilderShaders.Lit;
    if(shader!=null){
     var mat=new Material(shader);
     mat.color=piece.kind=="terrain"?new Color(.30f,.52f,.27f):new Color(.60f,.61f,.60f);
     if(piece.kind=="terrain"){
      if(!string.IsNullOrEmpty(piece.finish)&&piece.finish.StartsWith("texture:",StringComparison.Ordinal)){
       Texture2D image=GetMaterialImage(piece.finish.Substring(8));
       if(image!=null){mat.mainTexture=image;mat.mainTextureScale=new Vector2(piece.textureTile,piece.textureTile);}
      }else if(!string.IsNullOrEmpty(piece.finish)){
       int i=Array.IndexOf(FinishNames,piece.finish);
       if(i>=0)mat.color=FinishColors[i];
      }
     }
     renderer.sharedMaterial=mat;
    }
    var collider=meshObj.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
    meshObj.transform.SetPositionAndRotation(piece.position,piece.rotation);
    return meshObj;
   }
   if(piece.kind=="water"){
    GameObject water=GameObject.CreatePrimitive(PrimitiveType.Cube);
    water.name="WB_Water";
    water.transform.SetPositionAndRotation(piece.position+Vector3.up*.015f,piece.rotation);
    water.transform.localScale=piece.scale;
    water.GetComponent<Renderer>().sharedMaterial=
     WorldBuilderShaders.NewOverlay(new Color(.24f,.58f,.74f,.67f));
    // An actual sunken bottom and side banks, scaled with water depth.
    // Unlike a plain blue plane, this volume has a visible physical basin.
    var basin=new GameObject("Pool / shoreline interior");
    basin.transform.SetParent(water.transform,false);
    var basinFilter=basin.AddComponent<MeshFilter>();
    basinFilter.sharedMesh=BasinGeometry.CreateInterior();
    basin.AddComponent<MeshRenderer>().sharedMaterial=
     WorldBuilderShaders.NewLit(new Color(.68f,.62f,.50f));
    if(!string.IsNullOrEmpty(piece.finish)){
     Texture2D image=piece.finish.StartsWith("texture:",StringComparison.Ordinal)?
      GetMaterialImage(piece.finish.Substring(8)):null;
     if(image!=null){
      var mat=WorldBuilderShaders.NewLit(new Color(.74f,.91f,1f,.84f));
      mat.mainTexture=image;
      water.GetComponent<Renderer>().sharedMaterial=mat;
     }
    }
    return water;
   }
   if(piece.kind=="light-point"||piece.kind=="light-spot"){
    var holder=GameObject.CreatePrimitive(PrimitiveType.Sphere);
    holder.name="WB_"+piece.kind;
    holder.transform.SetPositionAndRotation(piece.position,piece.rotation);
    holder.transform.localScale=Vector3.one*.28f;
    var renderer=holder.GetComponent<Renderer>();
    if(renderer!=null)renderer.sharedMaterial=WorldBuilderShaders.NewLit(piece.lightColor);
    Light light=holder.AddComponent<Light>();
    light.type=piece.kind=="light-spot"?LightType.Spot:LightType.Point;
    light.range=Mathf.Max(1f,piece.lightRange);
    light.intensity=Mathf.Clamp(piece.lightIntensity,0f,5f);
    light.spotAngle=Mathf.Clamp(piece.lightAngle,1f,170f);
    light.color=piece.lightColor;
    light.shadows=LightShadows.None; // Editor preview keeps shadow cost low.
    return holder;
   }
   if(piece.kind=="imported"){
    try{
     string file=Path.Combine(AssetLibrary(),piece.assetId??"","model.obj");
     var objImported=ImportedObj.Load(file);
     ApplyImportedFinish(objImported,piece);
     if(piece.fitToSurface){
      var pivot=new GameObject("WB_Imported_"+piece.assetId);
      AnchorImportedModel(pivot,objImported,ModelBounds(objImported));
      pivot.transform.SetPositionAndRotation(piece.position,piece.rotation);
      pivot.transform.localScale=piece.scale;
      return pivot;
     }
     objImported.name="WB_Imported_"+piece.assetId;
     objImported.transform.SetPositionAndRotation(piece.position,piece.rotation);
     objImported.transform.localScale=piece.scale;
     return objImported;
    }catch(Exception e){
     Debug.LogError("Missing imported asset: "+e.Message);
     Diagnostics.Add("ERROR","Missing imported model "+piece.assetId+": "+e.Message);
     var missing=GameObject.CreatePrimitive(PrimitiveType.Cube);
     missing.name="MISSING ASSET "+piece.assetId;
     missing.transform.position=piece.position;
     missing.transform.localScale=Vector3.one*.45f;
     missing.GetComponent<Renderer>().sharedMaterial=WorldBuilderShaders.NewLit(new Color(.95f,.21f,.32f));
     return missing;
    }
   }
   GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
   obj.name="WB_"+piece.kind;
   // Assign a bundled material instead of relying on Unity's default primitive shader.
   obj.GetComponent<Renderer>().sharedMaterial=WorldBuilderShaders.NewLit(Color.white);
   // Older saves put floor tops at y=0, coplanar with the base plane.
   // Correct their render height without modifying the saved schema.
   Vector3 displayPosition=piece.position;
   if(piece.kind=="floor" && displayPosition.y < FloorCenter-.02f)displayPosition.y=FloorCenter;
   obj.transform.SetPositionAndRotation(displayPosition,piece.rotation);
   obj.transform.localScale=piece.scale;
   Material material=piece.kind=="wall"?wallMaterial:piece.kind=="floor"?floorMaterial:furnitureMaterial;
   if(!string.IsNullOrEmpty(piece.finish)&&piece.finish.StartsWith("texture:",StringComparison.Ordinal)){
    string id=piece.finish.Substring(8);
    Texture2D image=GetMaterialImage(id);
    if(image!=null){
     Shader shader=WorldBuilderShaders.Lit;
     if(shader!=null){
      Material finishMaterial=new Material(shader);
      finishMaterial.mainTexture=image;
      float repeat=Mathf.Clamp(piece.textureTile<=0?1f:piece.textureTile,.1f,20f);
      finishMaterial.mainTextureScale=new Vector2(repeat,repeat);
      obj.GetComponent<Renderer>().sharedMaterial=finishMaterial;
     }
    }else Diagnostics.Add("WARN","Missing texture material: "+id);
   }else if(!string.IsNullOrEmpty(piece.finish)&&piece.finish!="Default"){
    int colorIndex=Array.IndexOf(FinishNames,piece.finish);
    if(colorIndex>0){
     var renderer=obj.GetComponent<Renderer>();
     Shader shader=WorldBuilderShaders.Lit;
     if(shader!=null){var finishMaterial=new Material(shader);finishMaterial.color=FinishColors[colorIndex];renderer.sharedMaterial=finishMaterial;}
    }
   }else if(piece.kind=="stairs"){
    obj.GetComponent<Renderer>().material.color=new Color(.61f,.43f,.29f);
   }else if(piece.kind=="stair-rail"||piece.kind=="stair-post"){
    obj.GetComponent<Renderer>().material.color=new Color(.28f,.29f,.31f);
   }else if(piece.kind=="opening-frame"){
    obj.GetComponent<Renderer>().material.color=new Color(.66f,.62f,.58f);
   }else if(material!=null)obj.GetComponent<Renderer>().sharedMaterial=material;
   else obj.GetComponent<Renderer>().material.color=piece.kind=="wall"?new Color(.89f,.91f,.94f):piece.kind=="floor"?new Color(.47f,.52f,.58f):piece.kind=="water"?new Color(.18f,.47f,.75f):piece.kind=="path"?new Color(.47f,.43f,.38f):piece.kind=="vrc-mirror"?new Color(.55f,.72f,.82f):piece.kind=="vrc-spawn"?new Color(.22f,.86f,.40f):new Color(.25f,.6f,.7f);
   return obj;
  }
  void Erase(Vector3 point){
   SelectObject(-1);
   for(int i=objects.Count-1;i>=0;i--){
    if(objects[i]==null)continue;
    var collider=objects[i].GetComponent<Collider>();
    Vector3 closest=collider.ClosestPoint(point+Vector3.up*.5f);
    if(Vector3.Distance(closest,point+Vector3.up*.5f)<1f){
     RememberChange();Destroy(objects[i]);objects.RemoveAt(i);project.pieces.RemoveAt(i);return;
    }
   }
  }
  public bool Save(){
   try{
    RecoveryStore.WriteAtomic(filePath,JsonUtility.ToJson(project,true));
    if(recoverySessionStarted)RecoveryStore.ChangeProject(filePath);
    Debug.Log("Saved "+filePath);Notice("Saved: "+Path.GetFileName(filePath));return true;
   }
   catch(Exception e){Debug.LogError(e);Notice("Save failed: "+e.Message);return false;}
  }
  public void Load(){
   try{
    var loaded=JsonUtility.FromJson<Project>(File.ReadAllText(filePath));
    if(loaded==null||loaded.schema!=2||loaded.pieces==null)throw new InvalidDataException("Unsupported project schema");
    // Create first: never empty the user's active project on a failed load.
    var constructed=new List<GameObject>();
    try{
     foreach(var piece in loaded.pieces)constructed.Add(Create(piece));
    }catch{
     foreach(var candidate in constructed)if(candidate!=null)Destroy(candidate);
     throw;
    }
    SelectObject(-1);
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();objects.AddRange(constructed);
    project=loaded;
    ApplyEnvironment();Notice("Loaded: "+Path.GetFileName(filePath));
   }catch(Exception e){
    Debug.LogError("Load failed: "+e.Message);
    Diagnostics.Add("ERROR","Project could not be loaded: "+e.Message);
    if(interruptedRecovery==null)interruptedRecovery=RecoveryStore.ReadLatest(filePath);
    if(interruptedRecovery!=null){
     showRecoveryPrompt=true;
     Notice("The saved project could not load; a recovery copy is available.");
    }else Notice("Load failed: "+e.Message);
   }
  }
  // Premium rounded glass UI generated at runtime; no external font or art packages required.
  private GUIStyle panelStyle, headingStyle, captionStyle, menuStyle, activeStyle, regularStyle, sectionStyle, smallStyle;
  private GUIStyle trayStyle,cardStyle,selectedCardStyle,softStyle,subtleStyle,mutedStyle,miniStyle,shadowStyle,destructiveStyle,paletteSymbolStyle,cardTitleStyle,railStyle;
  private Texture2D darkTexture, sideTexture, blueTexture, fieldTexture, hoverTexture, strokeTexture;
  private Texture2D trayTexture,cardTexture,selectedCardTexture,softTexture,shadowTexture,destructiveTexture;
  private Font interfaceFont;
  private string selectedCatalogId="";
  private const float DesignWidth=1600f;
  float UIScale { get { return Mathf.Max(.65f,Mathf.Min(Screen.width/DesignWidth,Screen.height/850f)); } }
  // Centralize panel size so the HUD and pointer hit-testing remain in sync.
  float CatalogHeight(float height){return Mathf.Min(245f,height*.34f);}
  Texture2D Rounded(Color fill,Color border,int radius=20){
   const int size=64;
   var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
   tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
   float r=radius;
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    float dx=Mathf.Max(r-x-.5f,0f)+Mathf.Max(x-(size-r-.5f),0f);
    float dy=Mathf.Max(r-y-.5f,0f)+Mathf.Max(y-(size-r-.5f),0f);
    float d=Mathf.Sqrt(dx*dx+dy*dy);
    float outer=Mathf.Clamp01(r+1f-d);
    float inner=Mathf.Clamp01(r-1.25f-d);
    tex.SetPixel(x,y,Color.Lerp(Color.clear,Color.Lerp(border,fill,inner),outer));
   }
   tex.Apply();return tex;
  }
  GUIStyle RoundedStyle(Texture2D background,int fontSize=15){
   GUIStyle style=new GUIStyle(GUI.skin.button);
   style.normal.background=background;
   style.hover.background=hoverTexture;
   style.active.background=blueTexture;
   style.focused.background=background;
   style.border=new RectOffset(20,20,20,20);
   style.margin=new RectOffset(0,0,0,0);
   style.padding=new RectOffset(10,10,6,6);
   style.normal.textColor=new Color(.20f,.25f,.33f);
   style.hover.textColor=new Color(.12f,.19f,.28f);
   style.active.textColor=Color.white;
   style.focused.textColor=new Color(.20f,.25f,.33f);
   style.fontSize=fontSize;style.font=interfaceFont;
   style.alignment=TextAnchor.MiddleCenter;
   return style;
  }
  void InitStyles(){
   if(panelStyle!=null)return;
   interfaceFont=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},18);
   // Original WorldBuilder pearl / mist-lavender palette, inspired by lifestyle building games.
   darkTexture=Rounded(new Color(.952f,.956f,.979f,.94f),new Color(.77f,.81f,.89f,.83f),19);
   trayTexture=Rounded(new Color(.961f,.948f,.980f,.95f),new Color(.78f,.75f,.86f,.85f),20);
   sideTexture=Rounded(new Color(.975f,.978f,.989f,.92f),new Color(.76f,.81f,.88f,.79f),17);
   fieldTexture=Rounded(new Color(.993f,.994f,1f,.95f),new Color(.77f,.80f,.89f,.87f),16);
   cardTexture=Rounded(new Color(.997f,.997f,1f,.98f),new Color(.78f,.80f,.87f,.85f),17);
   selectedCardTexture=Rounded(new Color(.904f,.918f,1f,.97f),new Color(.39f,.53f,.88f,1f),17);
   softTexture=Rounded(new Color(.893f,.92f,.95f,.91f),new Color(.77f,.83f,.89f,.85f),16);
   shadowTexture=Rounded(new Color(.17f,.22f,.34f,.15f),new Color(.19f,.25f,.34f,.03f),20);
   blueTexture=Rounded(new Color(.32f,.43f,.80f,.99f),new Color(.54f,.66f,1f,.99f),17);
   hoverTexture=Rounded(new Color(.88f,.915f,1f,.98f),new Color(.54f,.65f,.92f,.98f),17);
   strokeTexture=Rounded(new Color(1f,1f,1f,.99f),new Color(.41f,.59f,.88f,.99f),16);
   destructiveTexture=Rounded(new Color(.98f,.91f,.91f,.98f),new Color(.92f,.66f,.67f,.93f),16);
   GUI.skin.font=interfaceFont;
   panelStyle=new GUIStyle(GUI.skin.box);
   panelStyle.normal.background=darkTexture;
   panelStyle.border=new RectOffset(20,20,20,20);
   panelStyle.normal.textColor=new Color(.16f,.22f,.30f);
   panelStyle.alignment=TextAnchor.UpperCenter;
   panelStyle.font=interfaceFont;panelStyle.fontSize=15;
   trayStyle=new GUIStyle(panelStyle);trayStyle.normal.background=trayTexture;
   cardStyle=new GUIStyle(panelStyle);cardStyle.normal.background=cardTexture;
   selectedCardStyle=new GUIStyle(panelStyle);selectedCardStyle.normal.background=selectedCardTexture;
   shadowStyle=new GUIStyle(panelStyle);shadowStyle.normal.background=shadowTexture;
   headingStyle=new GUIStyle(GUI.skin.label);
   headingStyle.font=interfaceFont;headingStyle.fontStyle=FontStyle.Bold;
   headingStyle.fontSize=22;headingStyle.normal.textColor=new Color(.14f,.21f,.33f);
   captionStyle=new GUIStyle(GUI.skin.label);
   captionStyle.font=interfaceFont;captionStyle.fontSize=15;
   captionStyle.normal.textColor=new Color(.39f,.45f,.56f);
   sectionStyle=new GUIStyle(captionStyle);
   sectionStyle.normal.textColor=new Color(.21f,.28f,.42f);
   sectionStyle.fontSize=17;sectionStyle.fontStyle=FontStyle.Bold;
   smallStyle=new GUIStyle(captionStyle);smallStyle.fontSize=13;
   paletteSymbolStyle=new GUIStyle(headingStyle);paletteSymbolStyle.fontSize=36;paletteSymbolStyle.alignment=TextAnchor.MiddleCenter;
   paletteSymbolStyle.normal.textColor=new Color(.31f,.43f,.69f);
   cardTitleStyle=new GUIStyle(headingStyle);cardTitleStyle.fontSize=14;
   mutedStyle=new GUIStyle(captionStyle);mutedStyle.fontSize=12;
   miniStyle=new GUIStyle(captionStyle);miniStyle.fontSize=14;miniStyle.fontStyle=FontStyle.Bold;
   menuStyle=RoundedStyle(sideTexture,15);
   softStyle=RoundedStyle(softTexture,15);
   railStyle=RoundedStyle(softTexture,12);
   GUI.skin.horizontalScrollbar.normal.background=softTexture;
   GUI.skin.horizontalScrollbar.hover.background=softTexture;
   GUI.skin.horizontalScrollbarThumb.normal.background=blueTexture;
   GUI.skin.horizontalScrollbarThumb.hover.background=blueTexture;
   GUI.skin.horizontalScrollbar.fixedHeight=9f;
   GUI.skin.verticalScrollbar.normal.background=softTexture;
   GUI.skin.verticalScrollbar.hover.background=softTexture;
   GUI.skin.verticalScrollbarThumb.normal.background=blueTexture;
   GUI.skin.verticalScrollbarThumb.hover.background=blueTexture;
   GUI.skin.verticalScrollbar.fixedWidth=9f;
   GUI.skin.horizontalScrollbarLeftButton.fixedWidth=0f;
   GUI.skin.horizontalScrollbarRightButton.fixedWidth=0f;
   GUI.skin.verticalScrollbarUpButton.fixedHeight=0f;
   GUI.skin.verticalScrollbarDownButton.fixedHeight=0f;
   activeStyle=RoundedStyle(blueTexture,15);
   activeStyle.normal.textColor=Color.white;
   activeStyle.hover.textColor=Color.white;
   activeStyle.focused.textColor=Color.white;
   subtleStyle=RoundedStyle(fieldTexture,14);
   destructiveStyle=RoundedStyle(destructiveTexture,15);
   regularStyle=new GUIStyle(GUI.skin.textField);
   regularStyle.normal.background=fieldTexture;
   regularStyle.focused.background=strokeTexture;
   regularStyle.border=new RectOffset(18,18,18,18);
   regularStyle.padding=new RectOffset(14,12,7,7);
   regularStyle.normal.textColor=new Color(.15f,.23f,.36f);
   regularStyle.focused.textColor=new Color(.15f,.23f,.36f);
   regularStyle.font=interfaceFont;regularStyle.fontSize=15;
  }
  void SoftPanel(Rect rect,GUIStyle style=null){
   GUI.Box(new Rect(rect.x,rect.y+4f,rect.width,rect.height),"",shadowStyle);
   GUI.Box(rect,"",style??panelStyle);
  }
  bool Button(Rect rect,string label,bool selected=false){
   return GUI.Button(rect,label,selected?activeStyle:menuStyle);
  }
  void Label(Rect rect,string content,bool heading=false){
   GUI.Label(rect,content,heading?headingStyle:captionStyle);
  }
  void ComingSoon(string what){Notice(what+" - coming soon");}
  void EnterWalk(){
   if(walkthrough!=null){
    if(preview!=null)preview.SetActive(false);
    showPause=false;
    walkthrough.SetHeight(avatarHeight);walkthrough.Enter();
   }else Notice("Walkthrough camera unavailable");
  }
  void SwitchMode(int mode){
   if(mode==3){EnterWalk();return;}
   if(mode!=creativeMode)catalogOpenForMode[creativeMode]=showCatalog;
   creativeMode=mode;
   showStudio=false;
   showCatalog=mode==0?false:catalogOpenForMode[mode];
   showInspector=selectedIndex>=0;
   if(mode==0){
    showCatalog=false;SelectTool(Tool.Select);
    Notice("Build mode · shape your world");
   }else if(mode==1){
    SelectTool(Tool.Select);
    Notice("Decorate mode · browse furniture and materials");
   }else{
    SelectTool(Tool.Terrain);
    Notice("Landscape mode · sculpt the outdoors");
   }
  }
  void DrawToolbar(float w){
   SoftPanel(new Rect(9,8,w-18,75),panelStyle);
   if(Button(new Rect(20,19,51,49),"⌂"))showPause=true;
   GUI.Label(new Rect(87,15,222,31),"WorldBuilder",headingStyle);
   GUI.Label(new Rect(88,47,272,25),"YOUR CREATIVE STUDIO  /  VRCHAT",mutedStyle);
   float controlWidth=418f;
   float groupWidth=Mathf.Min(568f,Mathf.Max(350f,w-controlWidth-412f));
   float middle=Mathf.Max(344f,(w-groupWidth)*.5f-35f);
   string[] titles={"BUILD","DECORATE","LANDSCAPE","LIVE"};
   for(int k=0;k<titles.Length;k++){
    int chosen=k;
    float cell=(groupWidth-20f)/4f;
    Rect spot=new Rect(middle+k*cell,20f,cell-7f,46f);
    if(Button(spot,titles[k],creativeMode==k))SwitchMode(chosen);
   }
   float actions=w-447f;
   if(Button(new Rect(actions,22,47,42),"↶"))UndoLast();
   if(Button(new Rect(actions+52,22,47,42),"↷"))RedoLast();
   if(Button(new Rect(actions+104,22,67,42),"SAVE",true))Save();
   if(Button(new Rect(actions+177,22,82,42),"STUDIO")){
    showStudio=!showStudio;
    studioTab=creativeMode==2?2:creativeMode==1?1:0;
   }
   if(Button(new Rect(actions+265,22,86,42),"DETAILS"))showInspector=!showInspector;
   if(Button(new Rect(actions+357,22,80,42),"MENU"))showPause=!showPause;
  }
  void SetCameraView(bool overhead){
   var orbit=sceneCamera!=null?sceneCamera.GetComponent<OrbitCamera>():null;
   if(orbit==null){Notice("Camera controls unavailable");return;}
   orbit.pitch=overhead?85f:48f;
   if(overhead)orbit.yaw=0f;
   orbit.distance=Mathf.Clamp(orbit.distance,8f,40f);
   Notice(overhead?"Top-down camera":"Perspective camera");
  }
  void DrawStudio(float w,float h){
   float x=Mathf.Clamp((w-650f)*.5f,520f,Mathf.Max(520f,w-665f)),y=163f;
   SoftPanel(new Rect(x,y,630,285),trayStyle);
   GUI.Label(new Rect(x+19,y+9,380,30),"DESIGN STUDIO",sectionStyle);
   if(Button(new Rect(x+568,y+6,45,32),"X")){showStudio=false;return;}
   string[] tabs={"Architecture","Lighting","Landscape","VRChat"};
   for(int i=0;i<tabs.Length;i++){int k=i;if(Button(new Rect(x+15+i*153,y+42,145,35),tabs[i],studioTab==i))studioTab=k;}
   float line=y+89;
   if(studioTab==0){
    if(Button(new Rect(x+15,line,188,40),polygonMode?"Room: Custom":"Room: Rectangle")){
     polygonMode=!polygonMode;polygonCorners.Clear();SelectTool(Tool.Room);
     showStudio=false;
     Notice(polygonMode?"Click corners, press Enter to finish":"Rectangular room tool selected");
    }
    if(Button(new Rect(x+215,line,185,40),"Garage Opening")){
     SelectTool(Tool.Garage);showStudio=false;showInspector=true;
    }
    if(Button(new Rect(x+413,line,195,40),"Custom Opening")){SelectTool(Tool.Door);showStudio=false;showInspector=true;}
    Label(new Rect(x+22,line+48,580,25),"Opening width: "+openingWidth.ToString("F2")+" m");
    openingWidth=GUI.HorizontalSlider(new Rect(x+28,line+75,555,21),openingWidth,.6f,5.5f);
    Label(new Rect(x+22,line+101,275,26),"Height: "+openingHeight.ToString("F2")+" m");
    openingHeight=GUI.HorizontalSlider(new Rect(x+24,line+130,257,19),openingHeight,.5f,4.2f);
    Label(new Rect(x+313,line+101,280,26),"Bottom: "+openingBottom.ToString("F2")+" m");
    openingBottom=GUI.HorizontalSlider(new Rect(x+315,line+130,270,19),openingBottom,0f,2f);
   }else if(studioTab==1){
    if(Button(new Rect(x+20,line,180,39),"Point Light")){lightMode=0;SelectTool(Tool.Light);showStudio=false;showInspector=true;}
    if(Button(new Rect(x+212,line,180,39),"Spot Light")){lightMode=1;SelectTool(Tool.Light);showStudio=false;showInspector=true;}
    if(Button(new Rect(x+405,line,190,39),"Night Sky")){project.skyPreset=2;ApplyEnvironment();}
    Label(new Rect(x+20,line+49,280,24),"Light intensity: "+lightBrightness.ToString("F1"));
    lightBrightness=GUI.HorizontalSlider(new Rect(x+24,line+76,268,22),lightBrightness,0f,8f);
    Label(new Rect(x+323,line+49,280,24),"Light range: "+lightRadius.ToString("F1")+" m");
    lightRadius=GUI.HorizontalSlider(new Rect(x+327,line+76,265,22),lightRadius,2f,30f);
    Label(new Rect(x+23,line+111,580,24),
     lightBrightness>2f?"High intensity can wash out textures. Recommended: 0.5–2.0.":
     "Click to place a light; select it later to fine-tune settings.");
    if(Button(new Rect(x+22,line+145,238,38),"BALANCE SCENE LIGHTS"))BalanceSceneLights();
    Label(new Rect(x+270,line+153,325,25),"Undo available · keeps positions");
   }else if(studioTab==2){
    if(Button(new Rect(x+18,line,186,42),"Terrain Hill")){SelectTool(Tool.Terrain);showStudio=false;}
    if(Button(new Rect(x+212,line,186,42),"Water")){SelectTool(Tool.Water);showStudio=false;showInspector=true;}
    if(Button(new Rect(x+406,line,188,42),"Fog: "+(project.fog?"On":"Off"))){project.fog=!project.fog;ApplyEnvironment();}
    string[] names={"Day","Sunset","Night","Overcast"};
    for(int i=0;i<4;i++){int k=i;if(Button(new Rect(x+18+i*146,line+53,138,36),names[i],project.skyPreset==i)){project.skyPreset=k;ApplyEnvironment();}}
    if(Button(new Rect(x+18,line+95,140,32),"Plant Tree")){SelectTool(Tool.Tree);showStudio=false;}
    if(Button(new Rect(x+168,line+95,140,32),"Path / Driveway")){SelectTool(Tool.Path);showStudio=false;}
    Label(new Rect(x+19,line+133,300,24),"Terrain elevation: "+terrainAmplitude.ToString("F1")+" m");
    terrainAmplitude=GUI.HorizontalSlider(new Rect(x+22,line+160,285,19),terrainAmplitude,.25f,12f);
    Label(new Rect(x+330,line+133,265,24),"Sun angle: "+project.sunPitch.ToString("F0")+"°");
    float angle=GUI.HorizontalSlider(new Rect(x+334,line+160,255,19),project.sunPitch,5f,85f);
    if(Mathf.Abs(angle-project.sunPitch)>.01f){project.sunPitch=angle;ApplyEnvironment();}
   }else{
    if(Button(new Rect(x+18,line,190,42),"Mirror")){SelectTool(Tool.Mirror);showStudio=false;}
    if(Button(new Rect(x+218,line,190,42),"Seat / Station")){SelectTool(Tool.Seat);showStudio=false;}
    if(Button(new Rect(x+418,line,190,42),"Spawn Point")){SelectTool(Tool.Spawn);showStudio=false;}
    Label(new Rect(x+22,line+58,570,56),"Place VRChat object markers in the scene. Mirrors and seats need the VRChat Worlds SDK when exported to Unity.");
    Label(new Rect(x+22,line+117,570,35),"Use VRChat Creator Companion to validate and publish the export.");
   }
  }
  void DrawViewportControls(float h){
   float x=135f,y=99f;
   SoftPanel(new Rect(x-7,y-6,499,58),panelStyle);
   if(Button(new Rect(x,y,112,44),"3D VIEW"))SetCameraView(false);
   if(Button(new Rect(x+118,y,102,44),"TOP VIEW"))SetCameraView(true);
   if(Button(new Rect(x+226,y,105,44),showGrid?"GRID ON":"GRID OFF"))ToggleGrid();
   bool scenery=scenicBackdrop==null||scenicBackdrop.Visible;
   if(Button(new Rect(x+337,y,145,44),scenery?"MEADOW ON":"MEADOW OFF")){
    if(scenicBackdrop!=null)scenicBackdrop.SetVisible(!scenery);
   }
   // Floor level stays visible while building to avoid unexpectedly floating walls.
   GUI.Box(new Rect(x+499,y-6,200,58),"",panelStyle);
   if(Button(new Rect(x+507,y,40,44),"−"))buildingLevel=Mathf.Max(0,buildingLevel-1);
   GUI.Label(new Rect(x+551,y+12,91,26),"LEVEL "+(buildingLevel+1),miniStyle);
   if(Button(new Rect(x+646,y,43,44),"+"))buildingLevel=Mathf.Min(8,buildingLevel+1);
   // Always accessible, even if the bottom palette is collapsed.
   if(wideViewportAvailable()){
    string category=creativeMode==2?
      (currentTool==Tool.Tree?"Trees":currentTool==Tool.Water?"Water":
       currentTool==Tool.Terrain?"Terrain":currentTool==Tool.Path?"Outdoor":"Plants"):
      creativeMode==1?(currentTool==Tool.Paint?"Materials":
       currentTool==Tool.Light?"Lighting":"Furniture"):"Architecture";
    if(Button(new Rect(x+712,y,172,44),"FIND "+category.ToUpperInvariant()))
     ToggleAssetBrowser(category);
   }
  }
  bool wideViewportAvailable(){return Screen.width/UIScale>=1330f;}
  string ToolGuidance(){
   if(currentTool==Tool.Room&&polygonMode)return "Click corners to shape a room · Enter to finish · Backspace to undo corner";
   switch(currentTool){
    case Tool.Select:return "Click an object to edit it · Shift-click multiple objects";
    case Tool.Wall:return "Drag a wall, or click its start and end points";
    case Tool.Floor:return "Drag a floor, or click two opposite corners";
    case Tool.Room:return "Drag a room or click two corners · Freeform: points then Enter";
    case Tool.Garage:return "Aim at a wall · Click to create a garage-sized opening";
    case Tool.Door:case Tool.Window:return "Aim at a wall · Click to create an opening";
    case Tool.Imported:return "Move your mouse to preview · Q/E rotate · Click to place";
    case Tool.Paint:return "Choose a finish below · Click a wall or floor";
    case Tool.Terrain:return "Drag terrain, or click two corners · Hill height in Studio";
    case Tool.Water:return "Drag a pond or pool, or click two corners";
    case Tool.Path:return "Drag a path or driveway, or click two corners";
    case Tool.Tree:return "Click to plant trees";
    case Tool.Light:return "Click to add a light · Very bright lights can wash out textures";
    default:return "Click or drag in the world to use "+currentTool.ToString();
   }
  }
  void DrawGuidanceStrip(float w,float h){
   float top=showCatalog?h-CatalogHeight(h)-9f:h-9f;
   float width=Mathf.Min(w-300f,690f);
   float left=135f;
   float y=showCatalog?top-51f:top-110f;
   if(y<169f)return;
   GUI.Box(new Rect(left,y,width,42f),"",panelStyle);
   bool notification=Time.time<statusUntil;
   string message=notification?status:ToolGuidance();
   if(message.Length>91)message=message.Substring(0,88)+"...";
   GUI.Label(new Rect(left+16,y+9,width-27,26),message,miniStyle);
  }
  void ChooseCatalogCategory(int category){
   if(category<0||category>12)return;
   catalogCategory=category;
   catalogScroll=Vector2.zero;
   // Browsing furniture must not leave the material brush active.
   if(category==8)SelectTool(Tool.Paint);
   else if(currentTool==Tool.Paint)SelectTool(Tool.Select);
  }
  void ChoosePaletteTool(Tool tool){
   SelectTool(tool);
   if(tool==Tool.Imported||tool==Tool.Paint){creativeMode=1;showCatalog=true;}
  }
  void DrawLeft(float h){
   const float x=13f;
   float y=98f;
   if(!showLibrary){
    if(Button(new Rect(x,y,104,46),"TOOLS >"))showLibrary=true;
    return;
   }
   Tool[] tools;
   string[] labels;
   if(creativeMode==1){
    tools=new[]{Tool.Select,Tool.Imported,Tool.Imported,Tool.Paint,Tool.Paint,Tool.Light,Tool.Erase};
    labels=new[]{"SELECT","MY ASSETS","FURNITURE","MATERIALS","TEXTURES","LIGHTING","ERASE"};
   }else if(creativeMode==2){
    tools=new[]{Tool.Select,Tool.Terrain,Tool.Tree,Tool.Path,Tool.Water,Tool.Water,Tool.Erase};
    labels=new[]{"SELECT","TERRAIN","TREES","PATHS","WATER","SKYBOXES","ERASE"};
   }else{
    tools=new[]{Tool.Select,Tool.Wall,Tool.Floor,Tool.Room,Tool.Door,Tool.Window,Tool.Garage,Tool.Stairs,Tool.Roof,Tool.Erase};
    labels=new[]{"SELECT","WALLS","FLOORS","ROOMS","DOORS","WINDOWS","GARAGE","STAIRS","ROOFS","ERASE"};
   }
   float shelfTop=showCatalog?h-CatalogHeight(h)-9f:h-9f;
   float availableHeight=Mathf.Max(225f,shelfTop-y-13f);
   float space=Mathf.Clamp((availableHeight-84f)/tools.Length,31f,45f);
   float railHeight=Mathf.Min(availableHeight,tools.Length*space+82f);
   SoftPanel(new Rect(x,y,109,railHeight),trayStyle);
   GUI.Label(new Rect(x+10,y+13,89,21),creativeMode==0?"BUILD":creativeMode==1?"DECOR":"GARDEN",miniStyle);
   if(Button(new Rect(x+10,y+40,89,28),"HIDE  <")){showLibrary=false;return;}
   float row=y+76f;
   int maxCount=Mathf.Min(tools.Length,Mathf.Max(0,Mathf.FloorToInt((railHeight-76f)/space)));
   for(int k=0;k<maxCount;k++){
    Tool choice=tools[k];
    bool isActive=creativeMode==1?(choice==Tool.Select&&k==0&&currentTool==Tool.Select)||
     (k==1&&showCatalog&&catalogCategory==0)||
     (k==2&&showCatalog&&catalogCategory==1)||
     ((k==3&&!textureOnly||k==4&&textureOnly)&&showCatalog&&catalogCategory==8)||
     (k==5&&showCatalog&&catalogCategory==2):
     creativeMode==2?(k==5&&showCatalog&&landscapeCategory==5):
     currentTool==choice;
    GUIStyle choiceStyle=isActive?activeStyle:choice==Tool.Erase?destructiveStyle:railStyle;
    if(GUI.Button(new Rect(x+9,row+k*space,91,space-4f),labels[k],choiceStyle)){
     if(creativeMode==1&&k>=1&&k<=5){
      showCatalog=true;catalogOpenForMode[1]=true;
      catalogCategory=k==1?0:k==2?1:k==5?2:8;
      textureOnly=k==4;
      if(k==3||k==4)SelectTool(Tool.Paint);
      else SelectTool(Tool.Select);
      Notice("Browsing "+labels[k].ToLowerInvariant());
     }else if(creativeMode==2&&k==5){
      showCatalog=true;catalogOpenForMode[2]=true;landscapeCategory=5;
      SelectTool(Tool.Select);
      Notice("Choose an HDRI skybox or find a new one");
     }else{
      SelectTool(choice);
      if(creativeMode==2){landscapeCategory=k;}
      if(choice==Tool.Light||choice==Tool.Water){showStudio=false;showInspector=true;}
     }
    }
   }
  }
  void DrawProperties(float w,float h){
   float x=w-364f,top=96f,panelW=350f;
   float catalogHeight=showCatalog?CatalogHeight(h)+12f:72f;
   float panelH=Mathf.Max(185f,h-top-catalogHeight-13f);
   SoftPanel(new Rect(x,top,panelW,panelH),trayStyle);
   if(Button(new Rect(x+295,top+15,39,36),"X")){showInspector=false;return;}
   bool selected=selectedIndex>=0&&selectedIndex<project.pieces.Count;
   GUI.Label(new Rect(x+22,top+16,265,30),selected?"OBJECT DETAILS":"TOOL SETTINGS",sectionStyle);
   string itemLabel=selected?project.pieces[selectedIndex].kind:currentTool.ToString();
   GUI.Label(new Rect(x+23,top+53,280,26),itemLabel.Replace('-',' ').ToUpperInvariant(),smallStyle);
   if(selectedIndex!=editingPiece){
    editingPiece=selectedIndex;
    if(selected){
     var original=project.pieces[selectedIndex];
     float[] value={original.position.x,original.position.y,original.position.z,
      original.scale.x,original.scale.y,original.scale.z};
     for(int i=0;i<6;i++)dimensionDraft[i]=value[i].ToString("F2");
    }
   }
   float contentH;
   if(selected){
    Piece selectedPiece=project.pieces[selectedIndex];
    contentH=270f;
    if(multiSelection.Count>1)contentH+=82f;
    if(selectedPiece.kind=="imported"||selectedPiece.kind=="couch"||selectedPiece.kind=="table")contentH+=selectedPiece.kind=="imported"?560f:130f;
    if(selectedPiece.kind=="wall")contentH+=176f;
    if(selectedPiece.kind=="water")contentH+=102f;
    if(selectedPiece.kind=="light-point"||selectedPiece.kind=="light-spot")contentH+=215f;
    if(IsRoomFloor(selectedIndex))contentH+=91f;
    if(inspectorAdvanced)contentH+=245f;
   }else{
    contentH=280f;
    if(currentTool==Tool.Wall)contentH+=175f;
    if(currentTool==Tool.Room)contentH+=140f;
    if(currentTool==Tool.Imported)contentH+=115f;
    if(currentTool==Tool.Light)contentH+=90f;
    if(currentTool==Tool.Water)contentH+=100f;
    if(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage)contentH+=225f;
   }
   Rect view=new Rect(x+12,top+92,panelW-25,Mathf.Max(120f,panelH-111f));
   inspectorScroll=GUI.BeginScrollView(view,inspectorScroll,new Rect(0,0,310,contentH));
   const float ox=7f;
   float y=12f;
   if(selected){
    var piece=project.pieces[selectedIndex];
    GUI.Label(new Rect(ox,y,284,24),"POSITION & ARRANGEMENT",miniStyle);y+=30;
    GUI.Label(new Rect(ox,y,286,50),"Drag the object in the scene, or use the move controls hovering above it.",captionStyle);y+=54;
    if(multiSelection.Count>1){
     GUI.Label(new Rect(ox,y,290,23),multiSelection.Count+" objects selected",sectionStyle);y+=29;
     if(Button(new Rect(ox,y,139,38),"GROUP"))GroupSelected();
     if(Button(new Rect(ox+148,y,139,38),"UNGROUP"))UngroupSelected();
     y+=49;
    }
    if(Button(new Rect(ox,y,139,40),"DUPLICATE"))DuplicateSelection();
    if(Button(new Rect(ox+148,y,139,40),"ROTATE 45°"))RotateSelection();
    y+=51;
    if(piece.kind=="imported"||piece.kind=="couch"||piece.kind=="table"){
     GUI.Label(new Rect(ox,y,280,27),"FURNITURE TRANSFORM",miniStyle);y+=31;
     if(Button(new Rect(ox,y,90,37),"↶ 15°"))RotateSelected(-15f);
     if(Button(new Rect(ox+98,y,90,37),"15° ↷"))RotateSelected(15f);
     if(Button(new Rect(ox+196,y,90,37),"+10%"))ResizeSelected(1.1f);
     y+=45;
     if(Button(new Rect(ox,y,138,37),"SCALE −10%"))ResizeSelected(.9f);
     if(Button(new Rect(ox+148,y,138,37),furnitureSnap?"SNAP  ON":"SNAP  OFF"))
      furnitureSnap=!furnitureSnap;
     y+=51;
     if(piece.kind=="imported"){
      if(Button(new Rect(ox,y,286,40),"FIT MODEL TO REAL SIZE"))FitSelectedImportedModel();
      y+=48;
      string id=piece.assetId;
      if(!string.IsNullOrEmpty(id)){
       GUI.Label(new Rect(ox,y,282,24),"ORGANIZE IN MY ASSETS",miniStyle);y+=29;
       string[] groups={"Furniture","Living Room","Bedroom","Kitchen","Bathroom",
        "Dining Room","Office","Outdoor","Lighting","Decor","Other","Trees"};
       int current=Array.IndexOf(groups,AssetHub.Category(id));
       int next=GUI.SelectionGrid(new Rect(ox,y,286,142),current,groups,3,railStyle);
       if(next>=0&&next!=current){
        try{AssetHub.SetCategory(id,groups[next]);Notice("Moved to "+groups[next]);}
        catch(Exception ex){Notice("Could not sort model: "+ex.Message);}
       }
       y+=156;
       var ownTextures=AssetHub.ModelTextures(id);
       GUI.Label(new Rect(ox,y,282,24),"TEXTURES WITH THIS MODEL",miniStyle);y+=29;
       if(Button(new Rect(ox,y,286,34),"RESTORE ORIGINAL MATERIALS"))
        SetImportedFinish(piece,"");
       y+=42;
       if(ownTextures.Count==0){
        GUI.Label(new Rect(ox,y,280,42),
         "No separate image textures were included with this model.",captionStyle);y+=48;
       }else{
        int max=Mathf.Min(ownTextures.Count,4);
        for(int k=0;k<max;k++){
         string texture=ownTextures[k];
         if(Button(new Rect(ox,y,286,34),"APPLY  "+(texture.Length>24?texture.Substring(0,21)+"...":texture)))
          SetImportedFinish(piece,"assettexture:"+texture);
         y+=38;
        }
        if(ownTextures.Count>4){
         GUI.Label(new Rect(ox,y,280,25),
          (ownTextures.Count-4)+" more textures in My Assets folder",captionStyle);y+=30;
        }
       }
      }
     }
    }
    if(piece.kind=="water"){
     GUI.Label(new Rect(ox,y,282,25),"WATER DEPTH",miniStyle);y+=29;
     GUI.Label(new Rect(ox,y,282,24),piece.scale.y.ToString("F2")+" m",smallStyle);y+=25;
     float depth=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),piece.scale.y,.15f,12f);y+=34;
     if(Mathf.Abs(depth-piece.scale.y)>.01f){
      RememberChange();
      float surface=piece.position.y+piece.scale.y*.5f;
      piece.scale.y=depth;
      piece.position.y=surface-depth*.5f;
      objects[selectedIndex].transform.position=piece.position+Vector3.up*.015f;
      objects[selectedIndex].transform.localScale=piece.scale;
     }
    }
    if(piece.kind=="wall"){
     GUI.Label(new Rect(ox,y,280,28),"WALL WORKSHOP",miniStyle);y+=35;
     if(Button(new Rect(ox,y,286,40),"STANDARD WALL HEIGHT"))SetStandardWallHeight();
     y+=47;
     if(Button(new Rect(ox,y,138,36),"COPY SIZE"))CopyWallSize();
     if(Button(new Rect(ox+148,y,138,36),"PASTE SIZE"))PasteWallSize();
     y+=43;
     if(Button(new Rect(ox,y,286,40),"BUILD WITH THIS WALL"))UseSelectedWallForBuilding();
     y+=51;
    }
    if(piece.kind=="floor"&&IsRoomFloor(selectedIndex)){
     if(Button(new Rect(ox,y,286,41),"RESIZE ENTIRE ROOM"))inspectorAdvanced=true;
     y+=49;
     GUI.Label(new Rect(ox,y,285,42),"Room dimensions are edited together with connected walls.",smallStyle);
     y+=42;
    }
    if(piece.kind=="light-point"||piece.kind=="light-spot"){
     GUI.Label(new Rect(ox,y,280,26),"LIGHTING",miniStyle);y+=33;
     GUI.Label(new Rect(ox,y,280,24),"Brightness  ·  "+piece.lightIntensity.ToString("F1"),smallStyle);y+=26;
     float brightness=GUI.HorizontalSlider(new Rect(ox+8,y,268,20),piece.lightIntensity,0f,8f);y+=31;
     GUI.Label(new Rect(ox,y,280,24),"Range  ·  "+piece.lightRange.ToString("F1")+" m",smallStyle);y+=26;
     float distance=GUI.HorizontalSlider(new Rect(ox+8,y,268,20),piece.lightRange,1f,30f);y+=38;
     if(Mathf.Abs(brightness-piece.lightIntensity)>.001f||Mathf.Abs(distance-piece.lightRange)>.001f){
      piece.lightIntensity=brightness;piece.lightRange=distance;
      Light light=objects[selectedIndex].GetComponent<Light>();
      if(light!=null){light.intensity=brightness;light.range=distance;}
     }
     if(Button(new Rect(ox,y,286,39),"TOGGLE WARM / WHITE")){
      RememberChange();
      piece.lightColor=piece.lightColor.r>piece.lightColor.b+.15f?
       Color.white:new Color(1f,.78f,.51f);
      Light component=objects[selectedIndex].GetComponent<Light>();
      if(component!=null)component.color=piece.lightColor;
     }
     y+=51;
    }
    if(Button(new Rect(ox,y,286,40),inspectorAdvanced?"HIDE EXACT VALUES":"EXACT POSITION & SIZE")){
     inspectorAdvanced=!inspectorAdvanced;
    }
    y+=46;
    if(inspectorAdvanced){
     GUI.Label(new Rect(ox,y,280,24),"POSITION  /  METERS",miniStyle);y+=29;
     float px=FloatField(new Rect(ox,y,90,40),0,piece.position.x);
     float py=FloatField(new Rect(ox+98,y,90,40),1,piece.position.y);
     float pz=FloatField(new Rect(ox+196,y,90,40),2,piece.position.z);
     y+=50;
     GUI.Label(new Rect(ox,y,280,24),"SIZE  /  METERS",miniStyle);y+=29;
     float sx=FloatField(new Rect(ox,y,90,40),3,piece.scale.x);
     float sy=FloatField(new Rect(ox+98,y,90,40),4,piece.scale.y);
     float sz=FloatField(new Rect(ox+196,y,90,40),5,piece.scale.z);
     y+=48;
     if(Button(new Rect(ox,y,286,43),"APPLY DIMENSIONS")){
      if(IsRoomFloor(selectedIndex)){ResizeRoom(selectedIndex,new Vector3(px,py,pz),sx,sz);}
      else{
       RememberChange();
       piece.position=new Vector3(px,py,pz);
       piece.scale=new Vector3(Mathf.Max(.02f,sx),Mathf.Max(.02f,sy),Mathf.Max(.02f,sz));
       if(objects[selectedIndex]!=null)Destroy(objects[selectedIndex]);
       objects[selectedIndex]=Create(piece);
       editingPiece=-1;RefreshSelectionOutline();
       Notice("Dimensions updated");
      }
     }
     y+=56;
    }
    if(GUI.Button(new Rect(ox,y,286,40),"DELETE SELECTED",destructiveStyle))DeleteSelection();
   }else{
    GUI.Label(new Rect(ox,y,286,47),"Choose a tool, then click or drag in the world to start creating.",captionStyle);y+=59;
    GUI.Label(new Rect(ox,y,286,26),"BUILDING LEVEL",miniStyle);y+=34;
    if(Button(new Rect(ox,y,139,40),"LEVEL  −"))buildingLevel=Mathf.Max(0,buildingLevel-1);
    if(Button(new Rect(ox+148,y,139,40),"LEVEL  +"))buildingLevel=Mathf.Min(8,buildingLevel+1);
    y+=47;
    GUI.Label(new Rect(ox,y,280,30),"Currently: Floor "+(buildingLevel+1),smallStyle);y+=43;
    GUI.Label(new Rect(ox,y,280,24),"GRID SNAP  ·  "+gridSize.ToString("F2")+" m",miniStyle);y+=28;
    gridSize=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),gridSize,.25f,2f);y+=44;
    if(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage){
     GUI.Label(new Rect(ox,y,282,25),"OPENING SETTINGS",miniStyle);y+=33;
     GUI.Label(new Rect(ox,y,282,24),"Width "+openingWidth.ToString("F2")+" m",smallStyle);y+=26;
     openingWidth=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),openingWidth,.5f,5.5f);y+=35;
     GUI.Label(new Rect(ox,y,282,24),"Height "+openingHeight.ToString("F2")+" m",smallStyle);y+=26;
     openingHeight=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),openingHeight,.5f,4.2f);y+=35;
     GUI.Label(new Rect(ox,y,282,24),"Sill height "+openingBottom.ToString("F2")+" m",smallStyle);y+=26;
     openingBottom=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),openingBottom,0f,2f);y+=41;
     GUI.Label(new Rect(ox,y,284,46),"Aim at a wall to make a "+currentTool.ToString().ToLowerInvariant()+" opening.",smallStyle);y+=53;
    }
    if(currentTool==Tool.Wall){
     GUI.Label(new Rect(ox,y,282,26),"WALL SETTINGS",miniStyle);y+=32;
     GUI.Label(new Rect(ox,y,282,24),"Height "+wallHeight.ToString("F2")+" m",smallStyle);y+=26;
     wallHeight=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),wallHeight,1.5f,5f);y+=39;
     GUI.Label(new Rect(ox,y,282,24),"Thickness "+wallThickness.ToString("F2")+" m",smallStyle);y+=26;
     wallThickness=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),wallThickness,.08f,.5f);y+=44;
     if(Button(new Rect(ox,y,286,41),"RESET WALL DEFAULTS"))ResetWallDefaults();
     y+=46;
    }
    if(currentTool==Tool.Room){
     GUI.Label(new Rect(ox,y,282,25),"ROOM SHAPE",miniStyle);y+=33;
     if(Button(new Rect(ox,y,286,41),polygonMode?"CUSTOM CORNERS":"RECTANGLE")){
      polygonMode=!polygonMode;polygonCorners.Clear();
     }
     y+=47;
     GUI.Label(new Rect(ox,y,282,51),"Custom: click corners and press Enter to complete.",smallStyle);y+=55;
    }
    if(currentTool==Tool.Imported){
     GUI.Label(new Rect(ox,y,282,25),"FURNITURE PLACEMENT",miniStyle);y+=33;
     GUI.Label(new Rect(ox,y,282,22),"Scale "+(importedScale/Mathf.Max(.000001f,importedFitBase)).ToString("F2")+"x  ·  fitted model",smallStyle);y+=27;
     importedScale=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),importedScale,
      Mathf.Max(.00001f,importedFitBase*.1f),importedFitBase*5f);y+=37;
     GUI.Label(new Rect(ox,y,282,24),"Rotation "+importedRotation.ToString("F0")+"°",smallStyle);y+=27;
     importedRotation=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),importedRotation,0f,360f);y+=40;
    }
    if(currentTool==Tool.Water){
     GUI.Label(new Rect(ox,y,282,25),"NEW WATER DEPTH",miniStyle);y+=32;
     GUI.Label(new Rect(ox,y,282,24),newWaterDepth.ToString("F2")+" m below the surface",smallStyle);y+=28;
     newWaterDepth=GUI.HorizontalSlider(new Rect(ox+8,y,268,20),newWaterDepth,.15f,12f);y+=32;
   }
   if(currentTool==Tool.Light){
     GUI.Label(new Rect(ox,y,282,25),"NEW LIGHT",miniStyle);y+=32;
     GUI.Label(new Rect(ox,y,282,22),"Intensity "+lightBrightness.ToString("F1"),smallStyle);y+=26;
     lightBrightness=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),lightBrightness,0f,8f);y+=37;
    }
    GUI.Label(new Rect(ox,y,282,24),"AVATAR PREVIEW HEIGHT",miniStyle);y+=28;
    GUI.Label(new Rect(ox,y,280,23),avatarHeight.ToString("F2")+" m",smallStyle);y+=24;
    avatarHeight=GUI.HorizontalSlider(new Rect(ox+8,y,270,20),avatarHeight,1.2f,2.2f);
   }
   GUI.EndScrollView();
  }
  float FloatField(Rect r,int index,float value){
   dimensionDraft[index]=GUI.TextField(r,dimensionDraft[index]??value.ToString("F2"),regularStyle);
   if(float.TryParse(dimensionDraft[index],out float number))return number;
   return value;
  }
  void DrawAssetTile(Rect tile,string title,string subtitle,int colorIndex,Action callback,Texture2D previewImage=null,bool selected=false,string fallbackLabel="3D MODEL"){
   GUI.Box(tile,"",selected?selectedCardStyle:cardStyle);
   Color[] fallback={
    new Color(.86f,.90f,.97f,.98f),new Color(.91f,.87f,.93f,.98f),
    new Color(.86f,.93f,.89f,.98f),new Color(.94f,.90f,.84f,.98f)};
   Rect imageBox=new Rect(tile.x+7,tile.y+7,tile.width-14,tile.height-56);
   GUI.DrawTexture(imageBox,previewImage!=null?previewImage:ThumbnailTexture(fallback[colorIndex%fallback.Length]),
    ScaleMode.ScaleToFit);
   if(previewImage==null)GUI.Label(new Rect(imageBox.x,imageBox.y+imageBox.height*.5f-20,imageBox.width,40),
    fallbackLabel,paletteSymbolStyle);
   string display=title.Length>22?title.Substring(0,19)+"...":title;
   GUI.Label(new Rect(tile.x+11,tile.y+tile.height-48,tile.width-19,24),display,cardTitleStyle);
   string provider=subtitle.Length>28?subtitle.Substring(0,25)+"...":subtitle;
   GUI.Label(new Rect(tile.x+11,tile.y+tile.height-24,tile.width-17,18),provider,mutedStyle);
   if(GUI.Button(tile,GUIContent.none,GUIStyle.none))callback();
  }
  void DrawPaletteTile(Rect tile,string glyph,string name,string subtitle,bool selected,Color tint,Action callback){
   GUI.Box(tile,"",selected?selectedCardStyle:cardStyle);
   GUI.DrawTexture(new Rect(tile.x+7,tile.y+7,tile.width-14,tile.height-63),
    ThumbnailTexture(tint),ScaleMode.StretchToFill);
   GUI.Label(new Rect(tile.x+12,tile.y+12,tile.width-24,tile.height-75),glyph,paletteSymbolStyle);
   GUI.Label(new Rect(tile.x+11,tile.y+tile.height-53,tile.width-17,25),name,cardTitleStyle);
   GUI.Label(new Rect(tile.x+11,tile.y+tile.height-27,tile.width-17,20),subtitle,mutedStyle);
   if(GUI.Button(tile,GUIContent.none,GUIStyle.none))callback();
  }
  private readonly Dictionary<int,Texture2D> catalogBackgrounds=new Dictionary<int,Texture2D>();
  private readonly Dictionary<string,Texture2D> modelThumbnails=new Dictionary<string,Texture2D>();
  Texture2D AssetThumbnail(string id){
   if(modelThumbnails.TryGetValue(id,out Texture2D image))return image;
   string path=Path.Combine(AssetLibrary(),id,"thumbnail.png");
   if(!File.Exists(path))return null;
   try{
    image=new Texture2D(2,2,TextureFormat.RGBA32,false);
    if(!image.LoadImage(File.ReadAllBytes(path))){Destroy(image);return null;}
    modelThumbnails[id]=image;return image;
   }catch(Exception ex){Debug.LogWarning("Thumbnail unreadable: "+ex.Message);return null;}
  }
  Texture2D ThumbnailTexture(Color color){
   int key=ColorUtility.ToHtmlStringRGBA(color).GetHashCode();
   if(!catalogBackgrounds.TryGetValue(key,out Texture2D result)){
    result=Rounded(color,new Color(.5f,.67f,.78f,.27f),12);
    catalogBackgrounds[key]=result;
   }
   return result;
  }
  void DrawSkyboxCollection(float left,float y,float viewport,float cardH){
   var skies=SkyboxLibrary.Catalog();
   var shown=new List<SkyboxLibrary.Entry>();
   foreach(var item in skies)if(catalogSearch.Length==0||
     (item.name+" "+item.provider).IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0)shown.Add(item);
   if(shown.Count==0){
    GUI.Label(new Rect(left+27,y+5,620,35),"No skyboxes downloaded yet",sectionStyle);
    GUI.Label(new Rect(left+27,y+40,650,26),
     "Find HDR panoramas, download a supported .hdr/.jpg/.png, and choose one here.",captionStyle);
    if(Button(new Rect(left+28,y+75,180,38),"FIND SKYBOXES"))ToggleAssetBrowser("Skyboxes");
    if(Button(new Rect(left+220,y+75,166,38),"USE GRADIENT"))SelectSkybox("");
    return;
   }
   const float spacing=168f;
   catalogScroll=GUI.BeginScrollView(new Rect(left+17,y,viewport,cardH+15),catalogScroll,
    new Rect(0,0,Mathf.Max(viewport-15,(shown.Count+1)*spacing+8),cardH),true,false);
   DrawAssetTile(new Rect(0,0,160,cardH-8),"Gradient Sky","Built-in · editable",0,
    ()=>SelectSkybox(""),null,string.IsNullOrEmpty(project.skyboxId),"DEFAULT");
   for(int k=0;k<shown.Count;k++){
    var sky=shown[k];
    DrawAssetTile(new Rect((k+1)*spacing,0,160,cardH-8),sky.name,
     "HDRI · "+sky.provider,k+1,()=>SelectSkybox(sky.id),null,
     project.skyboxId==sky.id,"HDRI SKY");
   }
   GUI.EndScrollView();
  }
  void ImportDownloadedAssetsSafely(){
   try{
    string result;
    int imported=AssetHub.Scan(out result);
    // Material and sky processing also have size limits; do not scan both
    // libraries immediately after large geometry imports in the same frame.
    if(imported==0){MaterialLibrary.Scan();SkyboxLibrary.Scan();}
    Notice(imported>0?"Imported "+imported+" model. Refresh again for the next.":"Library checked. "+result);
   }catch(Exception ex){
    Diagnostics.Add("ERROR","Manual asset discovery: "+ex.Message);
    Notice("Could not scan downloads: "+ex.Message);
   }
  }
  void DrawMyAssetCollection(float left,float y,float viewport,float cardH){
   if(Button(new Rect(left+19,y+4,210,34),"IMPORT DOWNLOADED ASSETS"))
    ImportDownloadedAssetsSafely();
   var models=AssetHub.Catalog();
   var materials=MaterialLibrary.Catalog();
   var skies=SkyboxLibrary.Catalog();
   var types=new List<int>();
   var indices=new List<int>();
   Func<string,bool> matching=name=>catalogSearch.Length==0||
    name.IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0;
   for(int i=0;i<models.Count;i++)
    if(matching(AssetHub.Name(models[i])+" "+AssetHub.Category(models[i])+" "+AssetHub.Provider(models[i]))){
     types.Add(0);indices.Add(i);
    }
   for(int i=0;i<materials.Count;i++)
    if(matching(materials[i].name+" Materials "+materials[i].provider)){
     types.Add(1);indices.Add(i);
    }
   for(int i=0;i<skies.Count;i++)
    if(matching(skies[i].name+" Skyboxes HDRI "+skies[i].provider)){
     types.Add(2);indices.Add(i);
    }
   if(types.Count==0){
    GUI.Label(new Rect(left+28,y+12,670,34),"Your library is ready for its first assets",sectionStyle);
    GUI.Label(new Rect(left+28,y+53,680,24),
     "Find models, materials or skyboxes. Downloads appear here automatically.",captionStyle);
    if(Button(new Rect(left+28,y+85,174,37),"FIND ASSETS"))ToggleAssetBrowser("Models");
    return;
   }
   const float cell=170f;
   catalogScroll=GUI.BeginScrollView(new Rect(left+17,y,viewport,cardH+15),catalogScroll,
    new Rect(0,0,Mathf.Max(viewport-15,types.Count*cell+8),cardH),true,false);
   for(int k=0;k<types.Count;k++){
    int i=indices[k];
    Rect tile=new Rect(k*cell,0,162,cardH-8);
    if(types[k]==0){
     string id=models[i];
     DrawAssetTile(tile,AssetHub.Name(id),AssetHub.Category(id)+" · "+AssetHub.Provider(id),
      k,()=>{selectedCatalogId=id;SelectImportedAsset(id);},
      AssetThumbnail(id),selectedCatalogId==id);
    }else if(types[k]==1){
     var m=materials[i];
     DrawAssetTile(tile,m.name,"Material · "+m.provider,k,()=>{
      catalogCategory=8;selectedTextureId=m.id;SelectTool(Tool.Paint);
      Notice("Material selected: "+m.name+" · click a wall or floor");
     },GetMaterialImage(m.id),selectedTextureId==m.id,"MATERIAL");
    }else{
     var sky=skies[i];
     DrawAssetTile(tile,sky.name,"Skybox · "+sky.provider,k,
      ()=>SelectSkybox(sky.id),null,project.skyboxId==sky.id,"HDRI SKY");
    }
   }
   GUI.EndScrollView();
  }
  void DrawCatalog(float w,float h){
   float shelfHeight=CatalogHeight(h);
   float top=h-shelfHeight-9f;
   float left=130f;
   float width=w-left-13f;
   SoftPanel(new Rect(left,top,width,shelfHeight),trayStyle);
   bool building=creativeMode==0,landscaping=creativeMode==2;
   string heading=building?"BUILD COLLECTION":landscaping?(landscapeCategory==5?"SKYBOXES":landscapeObjects?"OUTDOOR OBJECTS":"LANDSCAPE COLLECTION"):
     catalogCategory==0?"MY ASSETS":catalogCategory==8?(textureOnly?"TEXTURES":"SURFACE MATERIALS"):
     catalogCategory==12?"SKYBOXES & HDRI":"OBJECT COLLECTION";
   GUI.Label(new Rect(left+19,top+13,290,30),heading,sectionStyle);
   GUI.Label(new Rect(left+23,top+46,300,20),
    building?"Shapes, walls and openings":landscaping?"Create the world around your home":"Browse models, decor and finishes",mutedStyle);
   if(Button(new Rect(w-63,top+13,38,38),"−")){
    showCatalog=false;catalogOpenForMode[creativeMode]=false;return;
   }
   float cardY=top+103f;
   float cardH=Mathf.Max(123f,shelfHeight-118f);
   if(landscaping&&landscapeCategory==5){
    DrawSkyboxCollection(left,cardY,width-36f,cardH);
    return;
   }
   if(building||landscaping){
    if(landscaping){
     if(Button(new Rect(w-303,top+16,118,36),landscapeObjects?"LAND TOOLS":"MY PLANTS",landscapeObjects)){
      landscapeObjects=!landscapeObjects;
      catalogScroll=Vector2.zero;
     }
    }
    if(Button(new Rect(w-459,top+16,149,36),"FIND "+(currentTool==Tool.Tree?"TREES":currentTool==Tool.Water?"WATER":currentTool==Tool.Terrain?"TERRAIN":"ASSETS")))
     ToggleAssetBrowser(currentTool==Tool.Tree?"Trees":currentTool==Tool.Water?"Water":currentTool==Tool.Terrain?"Terrain":"Outdoor");
    if(landscaping&&landscapeObjects){
     var filtered=new List<string>();
     foreach(string id in AssetHub.Catalog()){
      string category=AssetHub.Category(id);
      if(category=="Plants"||category=="Outdoor"||category=="Trees")filtered.Add(id);
     }
     float viewWidth=width-36f;
     if(filtered.Count==0){
      GUI.Label(new Rect(left+24,cardY+9,680,36),
       "Your garden collection is ready for plants",sectionStyle);
      GUI.Label(new Rect(left+24,cardY+47,700,30),
       "Download plants or outdoor models; compatible assets appear here automatically.",captionStyle);
      if(Button(new Rect(left+24,cardY+89,173,42),"FIND PLANTS"))ToggleAssetBrowser("Plants");
     }else{
      const float spacing=170f;
      catalogScroll=GUI.BeginScrollView(new Rect(left+17,cardY,viewWidth,cardH+15),catalogScroll,
       new Rect(0,0,Mathf.Max(viewWidth-15,filtered.Count*spacing+7),cardH),true,false);
      for(int i=0;i<filtered.Count;i++){
       string id=filtered[i];
       DrawAssetTile(new Rect(i*spacing+1,0,162,cardH-7),AssetHub.Name(id),
        AssetHub.Category(id)+" · "+AssetHub.Provider(id),i,()=>{
         selectedCatalogId=id;SelectImportedAsset(id);
        },AssetThumbnail(id),selectedCatalogId==id);
      }
      GUI.EndScrollView();
     }
     return;
    }
    Tool[] available=building?
      new[]{Tool.Wall,Tool.Floor,Tool.Room,Tool.Room,Tool.Door,Tool.Window,Tool.Garage,Tool.Stairs,Tool.Roof,Tool.Select}:
      new[]{Tool.Terrain,Tool.Tree,Tool.Path,Tool.Water,Tool.Select};
    string[] names=building?
      new[]{"Wall","Floor","Room","Freeform","Doorway","Window","Garage","Stairs","Roof","Select"}:
      new[]{"Terrain","Trees","Path","Water","Select"};
    string[] glyphs=building?
      new[]{"▥","▤","▣","⬡","▯","⊞","▱","≋","⌂","◈"}:
      new[]{"▲","♣","▰","≈","◈"};
    string[] hints=building?
      new[]{"Draw walls","Draw flooring","Rectangle","Any shape","Wall opening","Place windows","Wide opening","Connect levels","Add roof","Edit objects"}:
      new[]{"Rolling hills","Place plants","Driveway","Pond or pool","Edit outdoors"};
    Color[] tint=building?
      new[]{new Color(.86f,.91f,.98f),new Color(.92f,.88f,.96f),new Color(.86f,.94f,.93f),
       new Color(.92f,.91f,.98f),new Color(.96f,.88f,.85f),new Color(.83f,.91f,.97f),
       new Color(.94f,.86f,.90f),new Color(.91f,.92f,.83f),new Color(.95f,.91f,.84f),new Color(.88f,.92f,.98f)}:
      new[]{new Color(.80f,.91f,.79f),new Color(.78f,.90f,.82f),new Color(.94f,.87f,.80f),
       new Color(.78f,.87f,.96f),new Color(.91f,.92f,.98f)};
    float cell=153f;
    float viewport=width-35f;
    catalogScroll=GUI.BeginScrollView(new Rect(left+16,cardY,viewport,cardH+17f),catalogScroll,
      new Rect(0,0,Mathf.Max(viewport-18,available.Length*cell+8f),cardH),true,false);
    for(int i=0;i<available.Length;i++){
     int index=i;
     Tool tool=available[i];
     bool selected=currentTool==tool&&(tool!=Tool.Room||polygonMode==(building&&i==3));
     DrawPaletteTile(new Rect(i*cell,1,146,cardH-8f),glyphs[i],names[i],hints[i],selected,tint[i],()=>{
      if(tool==Tool.Room){
       SelectTool(Tool.Room);
       polygonMode=building&&index==3;
      }else if(tool==Tool.Garage){
       SelectTool(Tool.Garage);openingWidth=3f;openingHeight=2.35f;openingBottom=0f;
      }else SelectTool(tool);
      Notice(tool==Tool.Room&&polygonMode?"Click room corners, press Enter to finish":
        names[index]+" selected");
     });
    }
    GUI.EndScrollView();
    return;
   }

   // The active left-hand collection owns the tray. Furniture is organized by
   // room, not by a second redundant master list of unrelated item types.
   string[] categories={"My Assets","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor",
    "Materials","Architecture","Other","Trees","Skyboxes"};
   float searchX=w-555f;
   if(searchX<left+410f)searchX=left+410f;
   GUI.Label(new Rect(searchX-75,top+20,75,24),"SEARCH",mutedStyle);
   catalogSearch=GUI.TextField(new Rect(searchX+3f,top+13f,157f,35f),catalogSearch,regularStyle);
   if(Button(new Rect(w-384,top+13,138,36),catalogCategory==12?"IMPORT SKY":catalogCategory==8?"IMPORT MAT.":"IMPORT 3D")){
    if(catalogCategory==12)ImportSkyFile();
    else if(catalogCategory==8)ImportMaterialFile();
    else{showImport=true;showExport=false;}
   }
   string[] rooms={"Living Room","Bedroom","Kitchen","Bathroom","Dining Room","Office","Outdoor","Other"};
   string browseCategory=catalogCategory==1?rooms[Mathf.Clamp(furnitureRoom,0,rooms.Length-1)]:
    catalogCategory==0?"Models":categories[catalogCategory];
   if(Button(new Rect(w-239,top+13,165,36),"FIND "+(catalogCategory==0?"ASSETS":browseCategory.ToUpperInvariant())))
    ToggleAssetBrowser(browseCategory);
   float categoryLeft=left+16,categoryWidth=width-44;
   string[] tabs=catalogCategory==1?rooms:new string[0];
   float total=0f;
   for(int i=0;i<tabs.Length;i++)total+=Mathf.Clamp(tabs[i].Length*9f+28f,92f,138f)+7f;
   catalogTabScroll=GUI.BeginScrollView(new Rect(categoryLeft,top+65,categoryWidth,37),
    catalogTabScroll,new Rect(0,0,Mathf.Max(categoryWidth-14,total+4f),34),true,false);
   float tabX=0f;
   for(int i=0;i<tabs.Length;i++){
    int which=i;float tabWidth=Mathf.Clamp(tabs[i].Length*9f+28f,92f,138f);
    if(Button(new Rect(tabX,0,tabWidth,31f),tabs[i],furnitureRoom==i)){
     furnitureRoom=which;catalogScroll=Vector2.zero;
    }
    tabX+=tabWidth+7f;
   }
   GUI.EndScrollView();
   float viewportWidth=width-36f;
   if(catalogCategory==0){
    DrawMyAssetCollection(left,cardY,viewportWidth,cardH);
   }else if(catalogCategory==12){
    DrawSkyboxCollection(left,cardY,viewportWidth,cardH);
   }else if(catalogCategory==8){
    var materials=MaterialLibrary.Catalog();
    float tileW=145f;
    int prefix=textureOnly?0:FinishNames.Length;
    int n=prefix+materials.Count;
    float sliderX=w-510f;
    GUI.Label(new Rect(sliderX,top+44,245,20),"Texture repeat: "+selectedTextureTile.ToString("F2")+"x",mutedStyle);
    float next=GUI.HorizontalSlider(new Rect(sliderX+237,top+49,155,17),selectedTextureTile,.25f,8f);
    selectedTextureTile=Mathf.Round(next*4f)/4f;
    catalogScroll=GUI.BeginScrollView(new Rect(left+17,cardY,viewportWidth,cardH+15),catalogScroll,
      new Rect(0,0,Mathf.Max(viewportWidth-15,n*tileW+10),cardH),true,false);
    for(int i=0;i<prefix;i++){
     int index=i;
     Rect tile=new Rect(i*tileW+2,0,tileW-8,cardH-7);
     bool selected=string.IsNullOrEmpty(selectedTextureId)&&selectedFinish==i;
     GUI.Box(tile,"",selected?selectedCardStyle:cardStyle);
     GUI.DrawTexture(new Rect(tile.x+7,tile.y+7,tile.width-14,tile.height-50),
      ThumbnailTexture(FinishColors[i]),ScaleMode.StretchToFill);
     GUI.Label(new Rect(tile.x+9,tile.y+tile.height-33,tile.width-15,27),FinishNames[i],cardTitleStyle);
     if(GUI.Button(tile,GUIContent.none,GUIStyle.none)){
      selectedTextureId="";selectedFinish=index;SelectTool(Tool.Paint);
     }
    }
    for(int i=0;i<materials.Count;i++){
     var material=materials[i];
     Rect tile=new Rect((i+prefix)*tileW+2,0,tileW-8,cardH-7);
     bool selected=selectedTextureId==material.id;
     GUI.Box(tile,"",selected?selectedCardStyle:cardStyle);
     Texture2D image=GetMaterialImage(material.id);
     if(image!=null)GUI.DrawTexture(new Rect(tile.x+7,tile.y+7,tile.width-14,tile.height-50),
       image,ScaleMode.ScaleAndCrop);
     GUI.Label(new Rect(tile.x+8,tile.y+tile.height-35,tile.width-15,22),
       material.name.Length>17?material.name.Substring(0,14)+"...":material.name,cardTitleStyle);
     GUI.Label(new Rect(tile.x+8,tile.y+tile.height-17,tile.width-15,16),material.provider,mutedStyle);
     if(GUI.Button(tile,GUIContent.none,GUIStyle.none)){selectedTextureId=material.id;SelectTool(Tool.Paint);}
    }
    GUI.EndScrollView();
   }else{
    var ids=AssetHub.Catalog();
    var visible=new List<string>();
    foreach(string id in ids){
     string category=catalogCategory==1?rooms[Mathf.Clamp(furnitureRoom,0,rooms.Length-1)]:
      catalogCategory==0?"All":categories[catalogCategory];
     string assetCategory=AssetHub.Category(id);
     if(category!="All"&&assetCategory!=category&&
      !(catalogCategory==1&&category=="Living Room"&&assetCategory=="Furniture"))continue;
     string keywords=AssetHub.Name(id)+" "+AssetHub.Category(id)+" "+AssetHub.Provider(id);
     if(catalogSearch.Length>0&&keywords.IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)<0)continue;
     visible.Add(id);
    }
    if(visible.Count==0){
     GUI.Label(new Rect(left+31,cardY+15,600,37),
      "No "+browseCategory.ToLowerInvariant()+" models in your library yet",sectionStyle);
     if(Button(new Rect(left+31,cardY+62,215,39),"FIND "+browseCategory.ToUpperInvariant()))
      ToggleAssetBrowser(browseCategory);
    }else{
     const float cell=170f;
     catalogScroll=GUI.BeginScrollView(new Rect(left+17,cardY,viewportWidth,cardH+15),catalogScroll,
       new Rect(0,0,Mathf.Max(viewportWidth-15,visible.Count*cell+4),cardH),true,false);
     for(int k=0;k<visible.Count;k++){
      string id=visible[k];
      string modelName=AssetHub.Name(id);
      if(modelName==id)modelName="Imported Asset "+(k+1);
      DrawAssetTile(new Rect(k*cell+1,0,162,cardH-7f),modelName,
       AssetHub.Category(id)+" · "+AssetHub.Provider(id),k,()=>{
        selectedCatalogId=id;SelectImportedAsset(id);
       },AssetThumbnail(id),selectedCatalogId==id);
     }
     GUI.EndScrollView();
    }
   }
  }
  void ExitApplication(bool saveFirst){
   if(quitting)return;
   if(saveFirst&&!Save()){Notice("Save failed; exit cancelled");return;}
   quitting=true;showConfirmExit=false;showPause=false;
   if(browserProcess!=null&&!browserProcess.HasExited)browserProcess.Kill();
   Application.Quit();
  }
  void DrawModalShade(float w,float h){
   Color previous=GUI.color;
   GUI.color=new Color(.10f,.14f,.23f,.55f);
   GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
   GUI.color=previous;
  }
  void DrawPause(float w,float h){
   DrawModalShade(w,h);
   float x=w*.5f-211,y=Mathf.Max(32f,h*.5f-297f);
   if(showConfirmExit){
    SoftPanel(new Rect(x,y+136,422,317),trayStyle);
    GUI.Label(new Rect(x+29,y+167,365,37),"LEAVE WORLDBUILDER?",sectionStyle);
    GUI.Label(new Rect(x+29,y+210,368,45),
     "Your latest automatic recovery is kept separately from the project file.",captionStyle);
    if(Button(new Rect(x+28,y+270,366,48),"SAVE & EXIT",true))ExitApplication(true);
    if(GUI.Button(new Rect(x+28,y+325,366,43),"EXIT WITHOUT SAVING",destructiveStyle))ExitApplication(false);
    if(Button(new Rect(x+28,y+375,366,43),"KEEP BUILDING"))showConfirmExit=false;
    return;
   }
   SoftPanel(new Rect(x,y,422,568),trayStyle);
   GUI.Label(new Rect(x+30,y+24,360,34),"WORLD BUILDER",headingStyle);
   GUI.Label(new Rect(x+32,y+60,356,27),"Your creative world is waiting",captionStyle);
   if(Button(new Rect(x+28,y+104,366,48),"CONTINUE BUILDING",true))showPause=false;
   if(Button(new Rect(x+28,y+158,366,43),"SAVE PROJECT"))Save();
   if(Button(new Rect(x+28,y+208,366,43),"WALK THROUGH WORLD"))EnterWalk();
   if(Button(new Rect(x+28,y+258,366,43),"EXPORT FOR VRCHAT")){showPause=false;showExport=true;}
   if(Button(new Rect(x+28,y+308,366,43),"MY PROJECTS & RECOVERY")){showPause=false;showProjects=true;}
   if(Button(new Rect(x+28,y+358,366,43),"SETTINGS & CONTROLS")){showPause=false;showSettings=true;}
   if(Button(new Rect(x+28,y+408,366,43),"START NEW WORLD")){showPause=false;NewProject();}
   if(GUI.Button(new Rect(x+28,y+466,366,43),"EXIT APPLICATION",destructiveStyle))showConfirmExit=true;
   GUI.Label(new Rect(x+33,y+523,350,24),"ESC  ·  Return to building",mutedStyle);
  }
  void DrawSettings(float w,float h){
   DrawModalShade(w,h);
   float x=w*.5f-300,y=Mathf.Max(26f,h*.5f-263f);
   SoftPanel(new Rect(x,y,600,524),trayStyle);
   GUI.Label(new Rect(x+26,y+20,540,33),"SETTINGS & SHORTCUTS",headingStyle);
   GUI.Label(new Rect(x+28,y+62,540,26),"Design faster with a few simple controls",captionStyle);
   GUI.Label(new Rect(x+30,y+105,540,28),"BUILD & DECORATE",sectionStyle);
   GUI.Label(new Rect(x+32,y+144,526,25),"Click or drag to place objects · Shift-click to select several",captionStyle);
   GUI.Label(new Rect(x+32,y+176,526,25),"C: custom rooms · Enter: finish · R: rotate · N: furniture snap",captionStyle);
   GUI.Label(new Rect(x+32,y+208,526,25),"Ctrl+D: duplicate · Ctrl+G: group · G: grid",captionStyle);
   GUI.Label(new Rect(x+30,y+255,540,27),"PROJECT & CAMERA",sectionStyle);
   GUI.Label(new Rect(x+32,y+292,526,25),"Ctrl+S: save · Ctrl+Z/Y: undo/redo · F2: diagnostics",captionStyle);
   GUI.Label(new Rect(x+32,y+324,526,25),"Right mouse: orbit · Scroll: zoom · T/P: camera views",captionStyle);
   GUI.Label(new Rect(x+30,y+369,540,28),"PEACE OF MIND",sectionStyle);
   GUI.Label(new Rect(x+32,y+405,527,27),"Automatic recovery snapshots protect work between manual saves.",smallStyle);
   if(Button(new Rect(x+28,y+459,544,43),"BACK TO BUILDING",true))showSettings=false;
  }
  void DrawDiagnostics(float w,float h){
   GUI.Box(new Rect(0,0,w,h),"",panelStyle);
   float x=Mathf.Max(25f,w*.5f-500f),y=Mathf.Max(35f,h*.5f-310f);
   float width=Mathf.Min(1000f,w-50f),height=Mathf.Min(620f,h-70f);
   GUI.Box(new Rect(x,y,width,height),"",panelStyle);
   Label(new Rect(x+25,y+19,width-180,35),"WorldBuilder Diagnostics  ·  F2",true);
   if(Button(new Rect(x+width-125,y+15,99,43),"Close"))showDiagnostics=false;
   Label(new Rect(x+27,y+65,width-50,27),
      "Automatic asset discovery   |   Errors: "+Diagnostics.Errors+"   Warnings: "+Diagnostics.Warnings);
   if(Button(new Rect(x+26,y+104,92,38),"All",diagnosticFilter=="All"))diagnosticFilter="All";
   if(Button(new Rect(x+126,y+104,112,38),"Errors",diagnosticFilter=="ERROR"))diagnosticFilter="ERROR";
   if(Button(new Rect(x+246,y+104,112,38),"Warnings",diagnosticFilter=="WARN"))diagnosticFilter="WARN";
   if(Button(new Rect(x+width-151,y+104,122,38),"Clear Log"))Diagnostics.Clear();
   float logY=y+152,viewHeight=height-208;
   var entries=Diagnostics.Entries;
   var visible=new List<Diagnostics.Entry>();
   for(int i=entries.Count-1;i>=0;i--)
    if(diagnosticFilter=="All"||entries[i].level==diagnosticFilter)visible.Add(entries[i]);
   float contentHeight=Mathf.Max(viewHeight,visible.Count*44f+12f);
   diagnosticsScroll=GUI.BeginScrollView(new Rect(x+23,logY,width-46,viewHeight),
      diagnosticsScroll,new Rect(0,0,width-72,contentHeight));
   if(visible.Count==0)GUI.Label(new Rect(16,14,width-130,32),
     "No messages. New imports and errors will appear here automatically.",captionStyle);
   for(int i=0;i<visible.Count;i++){
    var e=visible[i];
    string message="["+e.time+"]  "+e.level+"   "+e.message;
    GUI.Label(new Rect(16,i*44f+8,width-122,40),message,smallStyle);
   }
   GUI.EndScrollView();
   Label(new Rect(x+27,y+height-46,width-48,30),
     "Models: OBJ/GLB/glTF/ZIP · Materials: albedo/PBR ZIP · Skies: HDR/JPG/PNG · See My Assets.");
  }
  void DrawRecoveryPrompt(float w,float h){
   DrawModalShade(w,h);
   float width=Mathf.Min(660f,w-48f);
   float x=(w-width)*.5f,y=Mathf.Max(30f,(h-390f)*.5f);
   SoftPanel(new Rect(x,y,width,390f),trayStyle);
   Label(new Rect(x+30,y+23,width-60,40),"Recover your last build?",true);
   Label(new Rect(x+30,y+78,width-60,54),
      "WorldBuilder didn't close normally. A recovery copy of your work is available.");
   string projectName=interruptedRecovery==null?"Your world":Path.GetFileNameWithoutExtension(interruptedRecovery.sourcePath);
   string when="Last automatic snapshot";
   if(interruptedRecovery!=null){
    DateTime saved;
    if(DateTime.TryParse(interruptedRecovery.savedUtc,out saved))
     when="Saved "+saved.ToLocalTime().ToString("MMM d, h:mm tt")+"  ·  "+interruptedRecovery.objectCount+" objects";
   }
   GUI.Box(new Rect(x+28,y+146,width-56,87),"",panelStyle);
   Label(new Rect(x+46,y+158,width-92,30),projectName,true);
   Label(new Rect(x+46,y+191,width-92,27),when);
   if(Button(new Rect(x+28,y+255,width-56,48),"Restore My Build"))RestoreRecovery(true);
   if(Button(new Rect(x+28,y+308,width-56,42),"Open My Last Saved Project")){
    showRecoveryPrompt=false;
    if(interruptedRecovery!=null&&!string.IsNullOrWhiteSpace(interruptedRecovery.sourcePath)&&
       File.Exists(interruptedRecovery.sourcePath)){
     filePath=interruptedRecovery.sourcePath;
     if(recoverySessionStarted)RecoveryStore.ChangeProject(filePath);
     Load();
    }
    if(!showRecoveryPrompt)Notice("Saved project opened. Recovery is still available in Projects.");
   }
   Label(new Rect(x+30,y+356,width-60,26),"Your saved project won't be overwritten unless you choose Save.");
  }
  void OnGUI(){
   InitStyles();
   GUI.matrix=Matrix4x4.Scale(new Vector3(UIScale,UIScale,1f));
   float w=Screen.width/UIScale,h=Screen.height/UIScale;
   if(showRecoveryPrompt){DrawRecoveryPrompt(w,h);return;}
   if(walkthrough!=null&&walkthrough.IsWalking){
    GUI.Box(new Rect(0,0,w,48),"",panelStyle);
    Label(new Rect(19,10,Mathf.Min(w-35,840),30),"LIVE PREVIEW     WASD Move   |   Mouse Look   |   Esc Return");
    return;
   }
   // Diagnostics is a modal overlay to prevent accidental world edits.
   if(showDiagnostics){DrawDiagnostics(w,h);return;}
   // Modal dialogs own the screen. Never render interactive editor controls beneath them.
   if(showPause){DrawPause(w,h);return;}
   if(showSettings||showHelp){DrawSettings(w,h);return;}
   DrawToolbar(w);
   if(Diagnostics.Errors>0){
    if(Button(new Rect(w-558,91,145,36),"F2  Errors: "+Diagnostics.Errors))showDiagnostics=true;
   }
   DrawLeft(h);
   DrawViewportControls(h);
   DrawObjectHandles();
   if(showInspector)DrawProperties(w,h);
   if(showStudio)DrawStudio(w,h);
   if(showCatalog&&creativeMode!=0)DrawCatalog(w,h);
   if(showImport){
    float x=w*.5f-265;
    GUI.Box(new Rect(x,75,530,255),"",panelStyle);
    Label(new Rect(x+20,90,480,32),"Import 3D Model",true);
    Label(new Rect(x+20,126,490,23),"Choose OBJ, GLB, glTF or a ZIP containing a model.");
    importPath=GUI.TextField(new Rect(x+20,158,350,30),importPath,regularStyle);
    if(Button(new Rect(x+380,158,130,30),"Browse Files"))BrowseObj();
    if(Button(new Rect(x+20,206,235,36),"Import Model"))ImportObj();
    if(Button(new Rect(x+275,206,235,36),"Cancel"))showImport=false;
   }
   if(showExport){
    float x=w*.5f-265;
    GUI.Box(new Rect(x,75,530,245),"",panelStyle);
    Label(new Rect(x+20,93,480,30),"Prepare VRChat Export",true);
    Label(new Rect(x+20,129,490,36),"Existing VRChat Unity project path (optional):");
    targetVrcProject=GUI.TextField(new Rect(x+20,169,490,30),targetVrcProject,regularStyle);
    if(Button(new Rect(x+20,218,235,37),"Prepare Export"))ExportUnity();
    if(Button(new Rect(x+275,218,235,37),"Cancel"))showExport=false;
   }
   if(showProjects){
    float x=Mathf.Max(110,w*.5f-185);
    GUI.Box(new Rect(x,77,370,495),"",panelStyle);
    Label(new Rect(x+20,93,330,35),"Projects",true);
    if(Button(new Rect(x+15,131,340,34),"Reload last saved project"))Load();
    if(Button(new Rect(x+15,407,340,34),"Restore Current Project Autosave")){RestoreRecovery(false);showProjects=false;}
    if(Button(new Rect(x+15,448,340,34),"Recover Previous Crash")){RestoreRecovery(true);showProjects=false;}
    string[] paths=Directory.GetFiles(ProjectsFolder(),"*.json");
    Array.Sort(paths);Array.Reverse(paths);
    for(int k=0;k<Mathf.Min(paths.Length,5);k++){
     string path=paths[k];
     if(Button(new Rect(x+15,176+k*39,340,34),Path.GetFileNameWithoutExtension(path)))OpenProject(path);
    }
    if(Button(new Rect(x+15,367,340,29),"Close"))showProjects=false;
   }
   DrawGuidanceStrip(w,h);
  }
 }
}
