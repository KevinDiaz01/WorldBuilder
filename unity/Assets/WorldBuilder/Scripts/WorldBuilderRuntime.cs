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
  private int selectedFinish=0;
  private string selectedTextureId="";
  private float selectedTextureTile=1f;
  private readonly Dictionary<string,Texture2D> materialImages=new Dictionary<string,Texture2D>();
  private static readonly string[] FinishNames={"Default","Warm White","Stone","Charcoal","Sand","Terracotta","Sage","Ocean"};
  private static readonly Color[] FinishColors={Color.white,new Color(.91f,.88f,.80f),new Color(.58f,.62f,.63f),new Color(.19f,.23f,.28f),new Color(.77f,.67f,.51f),new Color(.67f,.34f,.26f),new Color(.38f,.52f,.43f),new Color(.22f,.47f,.62f)};
  private Vector2 catalogScroll;
  private string catalogSearch="";
  private int editingPiece=-1;
  private Vector3? copiedWallSize;
  private const float StandardWallHeight=2.7f;
  private readonly string[] dimensionDraft=new string[6];
  private string status="Ready to build";
  private float statusUntil;
  private bool showLibrary=true;
  private bool showCatalog=false;
  private bool showInspector=false;
  private bool showProjects=false;
  private GameObject preview;
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
  private float lightBrightness=1.6f;
  private float lightRadius=11f;
  private float terrainAmplitude=2.5f;
  private Light editorSun;
  private readonly List<GameObject> gridMarkers=new List<GameObject>();
  bool OverUI(){
   if(walkthrough!=null&&walkthrough.IsWalking)return true;
   if(showDiagnostics)return true;
   float scale=UIScale;
   float w=Screen.width/scale,h=Screen.height/scale;
   Vector2 p=new Vector2(Input.mousePosition.x/scale,(Screen.height-Input.mousePosition.y)/scale);
   return (objectHandleVisible&&objectHandleArea.Contains(p))||p.y<88f||(showCatalog&&p.y>h-272f)||
     (!showCatalog&&p.y>h-70f&&p.x<480f)||
     (showLibrary&&p.x<122f&&p.y>=94f&&p.y<735f)||
     (showInspector&&p.x>w-359f&&p.y>=94f&&p.y<h-175f)||
     (p.x>125f&&p.x<515f&&p.y<159f)||
     showPause||showConfirmExit||showSettings||showHelp||
     (showProjects&&p.x>w*.5f-200f&&p.x<w*.5f+210f&&p.y<505f)||
     ((showImport||showExport)&&p.x>w*.5f-275f&&p.x<w*.5f+275f&&p.y<335f)||
     showAssets||(showStudio&&p.x>515f&&p.x<1165f&&p.y>90f&&p.y<455f);
  }
  void ToggleAssetBrowser(){
   try{
    if(browserProcess!=null&&!browserProcess.HasExited){
     browserProcess.Kill();browserProcess.Dispose();browserProcess=null;
     showAssets=false;Notice("Assets browser closed");return;
    }
    string exe=Path.Combine(Application.dataPath,"AssetBrowser","WorldBuilderAssetBrowser.exe");
    if(!File.Exists(exe)){Notice("Asset browser component is missing. Reinstall the complete update.");return;}
    IntPtr hwnd=System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    if(hwnd==IntPtr.Zero){Notice("Unable to locate WorldBuilder window");return;}
    if(browserProcess!=null){browserProcess.Dispose();browserProcess=null;}
    browserProcess=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe,
      hwnd.ToInt64().ToString()+" "+QuoteArg(AssetHub.Root)){UseShellExecute=false});
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
   if(browserProcess!=null&&!browserProcess.HasExited)browserProcess.Kill();
   foreach(var tex in modelThumbnails.Values)if(tex!=null)Destroy(tex);
   modelThumbnails.Clear();
   foreach(var tex in materialImages.Values)if(tex!=null)Destroy(tex);
   materialImages.Clear();
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
    selectionOutline.material=new Material(Shader.Find("Sprites/Default"));
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
     line.material=new Material(Shader.Find("Sprites/Default"));
     line.positionCount=2;line.startWidth=.012f;line.endWidth=.012f;
     line.startColor=line.endColor=new Color(.45f,.6f,.7f,.28f);
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
     if(objects[i]==null||project.pieces[i].kind!="floor")continue;
     if(hit.collider.gameObject==objects[i]||hit.collider.transform.IsChildOf(objects[i].transform))
      return hit.point;
    }
   }
   return ground;
  }
  Vector3 ImportedPlacement(Vector3 ground){
   float lower=importedBoundsReady?importedBounds.min.y*importedScale:0f;
   Vector3 surface=PlacementSurface(ground);
   return new Vector3(surface.x,surface.y-lower,surface.z);
  }
  void UpdateImportedGhost(){
   if(string.IsNullOrEmpty(importedId))return;
   if(importedGhost!=null&&ghostAssetId==importedId)return;
   if(importedGhost!=null)Destroy(importedGhost);
   importedGhost=null;ghostAssetId="";
   try{
    importedGhost=ImportedObj.Load(Path.Combine(AssetLibrary(),importedId,"model.obj"));
    importedGhost.name="Furniture placement preview";
    foreach(var collider in importedGhost.GetComponentsInChildren<Collider>())Destroy(collider);
    foreach(var renderer in importedGhost.GetComponentsInChildren<Renderer>()){
     var original=renderer.sharedMaterials;
     var ghosts=new Material[original.Length];
     for(int i=0;i<original.Length;i++){
      Shader shader=Shader.Find("Standard");
      if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
      if(shader==null)shader=Shader.Find("Sprites/Default");
      ghosts[i]=new Material(shader);
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
   if(OverUI()||currentTool==Tool.Erase||currentTool==Tool.Paint||(!onGround&&!aimedAtWall)){
    if(importedGhost!=null)importedGhost.SetActive(false);
    if(preview!=null)preview.SetActive(false);
    return;
   }
   if(preview==null){
    preview=GameObject.CreatePrimitive(PrimitiveType.Cube);
    preview.name="Build cursor";
    Destroy(preview.GetComponent<Collider>());
    var m=new Material(Shader.Find("Sprites/Default"));
    m.color=new Color(.15f,.8f,1f,.5f);
    preview.GetComponent<Renderer>().material=m;
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
   preview.transform.rotation=Quaternion.identity;
   if(currentTool==Tool.Door||currentTool==Tool.Window||currentTool==Tool.Garage){
    if(!FindOpeningWall(out int wallIndex,out Vector3 hitPoint)){preview.SetActive(false);return;}
    Piece wall=project.pieces[wallIndex];
    bool window=currentTool==Tool.Window;
    float previewWidth=Mathf.Min(openingWidth,wall.scale.x-.2f);
    float previewHeight=Mathf.Min(openingHeight,wall.scale.y-.12f);
    float previewBottom=Mathf.Clamp(openingBottom,0f,wall.scale.y-previewHeight);
    Vector3 local=Quaternion.Inverse(wall.rotation)*(hitPoint-wall.position);
    local.x=Mathf.Clamp(local.x,-wall.scale.x*.5f+previewWidth*.5f+.08f,wall.scale.x*.5f-previewWidth*.5f-.08f);
    local.y=previewBottom+previewHeight*.5f-wall.scale.y*.5f;
    local.z=0;
    Vector3 forward=wall.rotation*Vector3.forward;
    float facing=Vector3.Dot(sceneCamera.transform.position-wall.position,forward)>=0?1f:-1f;
    preview.transform.position=wall.position+wall.rotation*local+forward*(facing*(wall.scale.z*.5f+.04f));
    preview.transform.rotation=wall.rotation;
    preview.transform.localScale=new Vector3(previewWidth,previewHeight,.07f);
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
  void SelectTool(Tool tool){currentTool=tool;anchor=null;polygonCorners.Clear();Notice(tool+" tool selected");}
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
    string id=AssetHub.Import(path,"Other");
    SelectImportedAsset(id);
    showImport=false;
    importError="";
    catalogCategory=0;showCatalog=true;
    Notice("Imported "+AssetHub.Name(id)+". Click the ground to place it.");
   }catch(Exception e){Debug.LogException(e);importError=e.Message;Notice("Import failed: "+e.Message);}
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
  void ApplyEnvironment(){
   if(sceneCamera!=null){
    sceneCamera.clearFlags=CameraClearFlags.SolidColor;
    Color[] palettes={new Color(.48f,.70f,.92f),new Color(.92f,.54f,.31f),new Color(.045f,.064f,.14f),new Color(.7f,.77f,.81f)};
    int sky=Mathf.Clamp(project.skyPreset,0,palettes.Length-1);
    sceneCamera.backgroundColor=palettes[sky];
    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight=sky==2?new Color(.19f,.24f,.38f):new Color(.58f,.62f,.69f);
    RenderSettings.fog=project.fog;
    RenderSettings.fogColor=palettes[sky];
    RenderSettings.fogDensity=.009f;
   }
   if(editorSun==null){
    var sun=new GameObject("WorldBuilder Studio Sun");
    editorSun=sun.AddComponent<Light>();
    editorSun.type=LightType.Directional;
   }
   editorSun.intensity=project.skyPreset==2?.14f:project.skyPreset==1?.68f:1.05f;
   editorSun.transform.rotation=Quaternion.Euler(project.sunPitch,project.sunYaw,0);
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
  void Update() {
   if(showRecoveryPrompt)return;
   if(Time.unscaledTime>=nextRecoveryTime){
    nextRecoveryTime=Time.unscaledTime+RecoveryInterval;
    WriteRecovery();
   }
   if(walkthrough!=null&&walkthrough.IsWalking)return;
   if(Input.GetKeyDown(KeyCode.F2)){showDiagnostics=!showDiagnostics;showPause=false;showHelp=false;showSettings=false;}
   if(showAssets&&(browserProcess==null||browserProcess.HasExited)){showAssets=false;if(browserProcess!=null){browserProcess.Dispose();browserProcess=null;}}
   if(Time.unscaledTime>=nextAssetScan){
    nextAssetScan=Time.unscaledTime+6f;
    string msg;
    int count=AssetHub.Scan(out msg);
    MaterialLibrary.Scan();
    if(msg.Length>0){assetStatus=msg;if(count>0)Notice(msg);}
    // Upgrade pre-existing imports with thumbnails, one model at a time.
    foreach(string id in AssetHub.Catalog()){
     string previewPath=Path.Combine(AssetLibrary(),id,"thumbnail.png");
     if(File.Exists(previewPath))continue;
     try{
      GameObject thumbnailModel=ImportedObj.Load(Path.Combine(AssetLibrary(),id,"model.obj"));
      ThumbnailUtility.Save(thumbnailModel,previewPath);
      Destroy(thumbnailModel);
     }catch(Exception ex){Debug.LogWarning("Could not preview "+id+": "+ex.Message);}
     break;
    }
   }
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
    }else{showPause=!showPause;anchor=null;}
   }
   if(Input.GetKeyDown(KeyCode.F1)){showHelp=!showHelp;showPause=false;showConfirmExit=false;}
   if(showDiagnostics||showPause||showConfirmExit||showSettings||showHelp||showImport||showExport||showProjects){
    if(importedGhost!=null)importedGhost.SetActive(false);
    if(preview!=null)preview.SetActive(false);
    return;
   }
   if(Input.GetKeyDown(KeyCode.G)&&!Input.GetKey(KeyCode.LeftControl)&&!Input.GetKey(KeyCode.RightControl))ToggleGrid();
   if(currentTool==Tool.Room&&Input.GetKeyDown(KeyCode.C)){polygonMode=!polygonMode;polygonCorners.Clear();anchor=null;Notice(polygonMode?"Custom room: click corners, Enter to finish":"Rectangular room mode");}
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
    var item=new Piece("imported",SnapFurniture(ImportedPlacement(p)),Vector3.one*importedScale,Quaternion.Euler(0,importedRotation,0));
    item.assetId=importedId;Add(item);return;
   }
   if(currentTool==Tool.Couch||currentTool==Tool.Table) {
    Add(new Piece(currentTool==Tool.Couch?"couch":"table",p+Vector3.up*.4f,new Vector3(currentTool==Tool.Couch?2f:1.2f,.8f,currentTool==Tool.Couch?.9f:1.2f),Quaternion.identity));return;
   }
   if(!anchor.HasValue){anchor=p;return;}
   Vector3 start=anchor.Value;anchor=null;
   if(Vector3.Distance(start,p)<.01f)return;
   if(currentTool==Tool.Room){MakeRoom(start,p);return;}
   if(currentTool==Tool.Terrain){Add(new Piece("terrain",(start+p)*.5f,new Vector3(Mathf.Max(2f,Mathf.Abs(start.x-p.x)),terrainAmplitude,Mathf.Max(2f,Mathf.Abs(start.z-p.z))),Quaternion.identity));return;}
   if(currentTool==Tool.Path){Add(new Piece("path",(start+p)*.5f+Vector3.up*.04f,new Vector3(Mathf.Max(.5f,Mathf.Abs(start.x-p.x)),.08f,Mathf.Max(.5f,Mathf.Abs(start.z-p.z))),Quaternion.identity));return;}
   if(currentTool==Tool.Water){Add(new Piece("water",(start+p)*.5f,new Vector3(Mathf.Max(1f,Mathf.Abs(start.x-p.x)),.08f,Mathf.Max(1f,Mathf.Abs(start.z-p.z))),Quaternion.identity));return;}
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
  void AddWithoutHistory(Piece piece){SelectObject(-1);project.pieces.Add(piece);objects.Add(Create(piece));}
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
   for(int k=0;k<steps;k++){
    float rise=LevelHeight*(k+1)/steps;
    Vector3 center=a+direction*((k+.5f)*stepRun)+Vector3.up*(buildingLevel*LevelHeight+rise*.5f);
    AddWithoutHistory(new Piece("stairs",center,new Vector3(stepRun+.02f,rise,1.2f),
     Quaternion.Euler(0,angle,0)));
   }
   Notice("Staircase created: "+steps+" steps");
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
   if(index<0){Notice("Choose a WorldBuilder surface to paint");return;}
   Piece p=project.pieces[index];
   if(p.kind!="wall"&&p.kind!="floor"&&p.kind!="roof"){Notice("Paint currently supports walls, floors and roofs");return;}
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
   float width=Mathf.Min(openingWidth,length-.20f);
   if(length<width+.3f||height<1.6f){Notice("Wall is too small for this opening");return;}
   Vector3 local=Quaternion.Inverse(original.rotation)*(wallPoint-original.position);
   float center=Mathf.Clamp(local.x,-length*.5f+width*.5f+.08f,length*.5f-width*.5f-.08f);
   float left=center-width*.5f,right=center+width*.5f;
   float openingHeight=Mathf.Min(this.openingHeight,height-.10f);
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
   trim(left-rim,left,bottom,bottom+openingHeight);
   trim(right,right+rim,bottom,bottom+openingHeight);
   trim(left-rim,right+rim,bottom+openingHeight,bottom+openingHeight+rim);
   if(window)trim(left-rim,right+rim,bottom-rim,bottom);
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
  GameObject Create(Piece piece){
   if(piece.kind=="tree"){
    GameObject root=new GameObject("WB_Tree");
    root.transform.SetPositionAndRotation(piece.position,piece.rotation);
    GameObject trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name="Tree Trunk";trunk.transform.SetParent(root.transform,false);
    trunk.transform.localPosition=new Vector3(0,1.05f,0);
    trunk.transform.localScale=new Vector3(.38f,1.05f,.38f);
    trunk.GetComponent<Renderer>().material.color=new Color(.34f,.21f,.13f);
    GameObject crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);
    crown.name="Tree Canopy";crown.transform.SetParent(root.transform,false);
    crown.transform.localPosition=new Vector3(0,2.9f,0);
    crown.transform.localScale=new Vector3(2.6f,2.5f,2.6f);
    crown.GetComponent<Renderer>().material.color=new Color(.20f,.43f,.22f);
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
    Shader shader=Shader.Find("Standard");
    if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
    if(shader!=null){var mat=new Material(shader);mat.color=piece.kind=="terrain"?new Color(.30f,.52f,.27f):new Color(.60f,.61f,.60f);renderer.sharedMaterial=mat;}
    var collider=meshObj.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
    meshObj.transform.SetPositionAndRotation(piece.position,piece.rotation);
    return meshObj;
   }
   if(piece.kind=="light-point"||piece.kind=="light-spot"){
    var holder=GameObject.CreatePrimitive(PrimitiveType.Sphere);
    holder.name="WB_"+piece.kind;
    holder.transform.SetPositionAndRotation(piece.position,piece.rotation);
    holder.transform.localScale=Vector3.one*.28f;
    var renderer=holder.GetComponent<Renderer>();
    if(renderer!=null)renderer.material.color=piece.lightColor;
    Light light=holder.AddComponent<Light>();
    light.type=piece.kind=="light-spot"?LightType.Spot:LightType.Point;
    light.range=Mathf.Max(1f,piece.lightRange);
    light.intensity=Mathf.Max(0,piece.lightIntensity);
    light.spotAngle=Mathf.Clamp(piece.lightAngle,1f,170f);
    light.color=piece.lightColor;
    light.shadows=LightShadows.Soft;
    return holder;
   }
   if(piece.kind=="imported"){
    try{
     string file=Path.Combine(AssetLibrary(),piece.assetId??"","model.obj");
     var objImported=ImportedObj.Load(file);
     objImported.name="WB_Imported_"+piece.assetId;
     objImported.transform.SetPositionAndRotation(piece.position,piece.rotation);
     objImported.transform.localScale=piece.scale;
     return objImported;
    }catch(Exception e){
     Debug.LogError("Missing imported asset: "+e.Message);
     var missing=GameObject.CreatePrimitive(PrimitiveType.Cube);
     missing.name="MISSING ASSET "+piece.assetId;missing.transform.position=piece.position;
     return missing;
    }
   }
   GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
   obj.name="WB_"+piece.kind;
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
     Shader shader=Shader.Find("Standard");
     if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
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
     Shader shader=Shader.Find("Standard");
     if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
     if(shader!=null){var finishMaterial=new Material(shader);finishMaterial.color=FinishColors[colorIndex];renderer.sharedMaterial=finishMaterial;}
    }
   }else if(piece.kind=="opening-frame"){
    obj.GetComponent<Renderer>().material.color=new Color(.15f,.75f,.94f);
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
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();SelectObject(-1);project=loaded;
    foreach(var piece in project.pieces)objects.Add(Create(piece));ApplyEnvironment();Notice("Loaded: "+Path.GetFileName(filePath));
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
  private Texture2D darkTexture, sideTexture, blueTexture, fieldTexture, hoverTexture, strokeTexture;
  private Font interfaceFont;
  private string selectedCatalogId="";
  private const float DesignWidth=1600f;
  float UIScale { get { return Mathf.Max(.65f,Mathf.Min(Screen.width/DesignWidth,Screen.height/850f)); } }
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
  GUIStyle RoundedStyle(Texture2D texture,int fontSize=17){
   var style=new GUIStyle(GUI.skin.button);
   style.normal.background=texture;style.hover.background=hoverTexture;
   style.active.background=blueTexture;style.focused.background=texture;
   style.border=new RectOffset(20,20,20,20);
   style.padding=new RectOffset(10,10,8,8);
   style.normal.textColor=Color.white;style.hover.textColor=Color.white;
   style.active.textColor=Color.white;style.focused.textColor=Color.white;
   style.fontSize=fontSize;style.font=interfaceFont;
   style.alignment=TextAnchor.MiddleCenter;
   return style;
  }
  void InitStyles(){
   if(panelStyle!=null)return;
   interfaceFont=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},18);
   darkTexture=Rounded(new Color(.052f,.079f,.115f,.87f),new Color(.40f,.55f,.68f,.52f));
   sideTexture=Rounded(new Color(.068f,.105f,.15f,.82f),new Color(.37f,.5f,.63f,.42f));
   blueTexture=Rounded(new Color(.08f,.34f,.70f,.94f),new Color(.35f,.73f,1f,.96f));
   hoverTexture=Rounded(new Color(.17f,.28f,.41f,.96f),new Color(.5f,.72f,.89f,.7f));
   fieldTexture=Rounded(new Color(.10f,.15f,.21f,.85f),new Color(.28f,.4f,.51f,.68f));
   strokeTexture=Rounded(new Color(.095f,.13f,.18f,.95f),new Color(.4f,.54f,.66f,.63f));
   GUI.skin.font=interfaceFont;
   panelStyle=new GUIStyle(GUI.skin.box);
   panelStyle.normal.background=darkTexture;
   panelStyle.border=new RectOffset(20,20,20,20);
   panelStyle.normal.textColor=Color.white;
   panelStyle.alignment=TextAnchor.UpperCenter;
   panelStyle.font=interfaceFont;panelStyle.fontSize=16;
   headingStyle=new GUIStyle(GUI.skin.label);
   headingStyle.font=interfaceFont;
   headingStyle.normal.textColor=new Color(.97f,.985f,1f);
   headingStyle.fontSize=23;headingStyle.fontStyle=FontStyle.Bold;
   captionStyle=new GUIStyle(GUI.skin.label);
   captionStyle.font=interfaceFont;
   captionStyle.normal.textColor=new Color(.75f,.83f,.91f);
   captionStyle.fontSize=16;
   sectionStyle=new GUIStyle(captionStyle);
   sectionStyle.normal.textColor=new Color(.94f,.97f,1f);
   sectionStyle.fontSize=17;sectionStyle.fontStyle=FontStyle.Bold;
   smallStyle=new GUIStyle(captionStyle);smallStyle.fontSize=14;
   menuStyle=RoundedStyle(sideTexture,17);
   activeStyle=RoundedStyle(blueTexture,17);
   regularStyle=new GUIStyle(GUI.skin.textField);
   regularStyle.normal.background=fieldTexture;
   regularStyle.focused.background=strokeTexture;
   regularStyle.border=new RectOffset(20,20,20,20);
   regularStyle.padding=new RectOffset(14,10,7,7);
   regularStyle.normal.textColor=Color.white;
   regularStyle.focused.textColor=Color.white;
   regularStyle.font=interfaceFont;regularStyle.fontSize=16;
  }
  bool Button(Rect rect,string label,bool selected=false){return GUI.Button(rect,label,selected?activeStyle:menuStyle);}
  void Label(Rect rect,string content,bool heading=false){GUI.Label(rect,content,heading?headingStyle:captionStyle);}
  void ComingSoon(string what){Notice(what+" - coming soon");}
  void EnterWalk(){
   if(walkthrough!=null){
    if(preview!=null)preview.SetActive(false);
    showPause=false;
    walkthrough.SetHeight(avatarHeight);walkthrough.Enter();
   }else Notice("Walkthrough camera unavailable");
  }
  void DrawToolbar(float w){
   GUI.Box(new Rect(10,9,w-20,73),"",panelStyle);
   if(Button(new Rect(21,19,55,53),"⌂"))showPause=true;
   Label(new Rect(92,16,235,31),"WorldBuilder",true);
   Label(new Rect(92,47,310,25),Path.GetFileNameWithoutExtension(filePath)+"  ·  VRChat world");
   float middle=w*.5f-285f;
   string[] modes={"Build","Decorate","Landscape","Live"};
   for(int k=0;k<modes.Length;k++){
    int n=k;
    if(Button(new Rect(middle+k*145,22,137,46),modes[k],creativeMode==k)){
     if(k==3)EnterWalk();
     else {creativeMode=n;if(n==2){showStudio=true;studioTab=2;}else if(n==1){showStudio=true;studioTab=1;}}
    }
   }
   if(Button(new Rect(w-403,22,64,46),"Undo"))UndoLast();
   if(Button(new Rect(w-333,22,67,46),"Redo"))RedoLast();
   if(Button(new Rect(w-260,22,70,46),"Save"))Save();
   if(Button(new Rect(w-183,22,80,46),"Studio"))showStudio=!showStudio;
   if(Button(new Rect(w-95,22,71,46),"Menu"))showPause=!showPause;
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
   GUI.Box(new Rect(x,y,630,285),"",panelStyle);
   Label(new Rect(x+19,y+9,380,30),"WorldBuilder 1.0 · Studios",true);
   if(Button(new Rect(x+568,y+6,45,32),"X")){showStudio=false;return;}
   string[] tabs={"Architecture","Lighting","Landscape","VRChat"};
   for(int i=0;i<tabs.Length;i++){int k=i;if(Button(new Rect(x+15+i*153,y+42,145,35),tabs[i],studioTab==i))studioTab=k;}
   float line=y+89;
   if(studioTab==0){
    if(Button(new Rect(x+15,line,188,40),polygonMode?"Room: Custom":"Room: Rectangle")){
     polygonMode=!polygonMode;polygonCorners.Clear();SelectTool(Tool.Room);
     Notice(polygonMode?"Click corners, press Enter to finish":"Rectangular room tool selected");
    }
    if(Button(new Rect(x+215,line,185,40),"Garage Opening")){
     SelectTool(Tool.Garage);openingWidth=3f;openingHeight=2.35f;openingBottom=0f;
    }
    if(Button(new Rect(x+413,line,195,40),"Custom Opening")){SelectTool(Tool.Door);}
    Label(new Rect(x+22,line+48,580,25),"Opening width: "+openingWidth.ToString("F2")+" m");
    openingWidth=GUI.HorizontalSlider(new Rect(x+28,line+75,555,21),openingWidth,.6f,5.5f);
    Label(new Rect(x+22,line+101,275,26),"Height: "+openingHeight.ToString("F2")+" m");
    openingHeight=GUI.HorizontalSlider(new Rect(x+24,line+130,257,19),openingHeight,.5f,4.2f);
    Label(new Rect(x+313,line+101,280,26),"Bottom: "+openingBottom.ToString("F2")+" m");
    openingBottom=GUI.HorizontalSlider(new Rect(x+315,line+130,270,19),openingBottom,0f,2f);
   }else if(studioTab==1){
    if(Button(new Rect(x+20,line,180,39),"Point Light")){lightMode=0;SelectTool(Tool.Light);}
    if(Button(new Rect(x+212,line,180,39),"Spot Light")){lightMode=1;SelectTool(Tool.Light);}
    if(Button(new Rect(x+405,line,190,39),"Night Sky")){project.skyPreset=2;ApplyEnvironment();}
    Label(new Rect(x+20,line+49,280,24),"Light intensity: "+lightBrightness.ToString("F1"));
    lightBrightness=GUI.HorizontalSlider(new Rect(x+24,line+76,268,22),lightBrightness,0f,8f);
    Label(new Rect(x+323,line+49,280,24),"Light range: "+lightRadius.ToString("F1")+" m");
    lightRadius=GUI.HorizontalSlider(new Rect(x+327,line+76,265,22),lightRadius,2f,30f);
    Label(new Rect(x+23,line+116,580,26),"Click the ground to place a light. Select it to adjust position and rotation.");
   }else if(studioTab==2){
    if(Button(new Rect(x+18,line,186,42),"Terrain Hill"))SelectTool(Tool.Terrain);
    if(Button(new Rect(x+212,line,186,42),"Water"))SelectTool(Tool.Water);
    if(Button(new Rect(x+406,line,188,42),"Fog: "+(project.fog?"On":"Off"))){project.fog=!project.fog;ApplyEnvironment();}
    string[] names={"Day","Sunset","Night","Overcast"};
    for(int i=0;i<4;i++){int k=i;if(Button(new Rect(x+18+i*146,line+53,138,36),names[i],project.skyPreset==i)){project.skyPreset=k;ApplyEnvironment();}}
    if(Button(new Rect(x+18,line+95,140,32),"Plant Tree"))SelectTool(Tool.Tree);
    if(Button(new Rect(x+168,line+95,140,32),"Path / Driveway"))SelectTool(Tool.Path);
    Label(new Rect(x+19,line+133,300,24),"Terrain elevation: "+terrainAmplitude.ToString("F1")+" m");
    terrainAmplitude=GUI.HorizontalSlider(new Rect(x+22,line+160,285,19),terrainAmplitude,.25f,12f);
    Label(new Rect(x+330,line+133,265,24),"Sun angle: "+project.sunPitch.ToString("F0")+"°");
    float angle=GUI.HorizontalSlider(new Rect(x+334,line+160,255,19),project.sunPitch,5f,85f);
    if(Mathf.Abs(angle-project.sunPitch)>.01f){project.sunPitch=angle;ApplyEnvironment();}
   }else{
    if(Button(new Rect(x+18,line,190,42),"Mirror"))SelectTool(Tool.Mirror);
    if(Button(new Rect(x+218,line,190,42),"Seat / Station"))SelectTool(Tool.Seat);
    if(Button(new Rect(x+418,line,190,42),"Spawn Point"))SelectTool(Tool.Spawn);
    Label(new Rect(x+22,line+58,570,56),"Place VRChat object markers in the scene. Mirrors and seats need the VRChat Worlds SDK when exported to Unity.");
    Label(new Rect(x+22,line+117,570,35),"Use VRChat Creator Companion to validate and publish the export.");
   }
  }
  void DrawViewportControls(float h){
   float x=132f,y=99f;
   GUI.Box(new Rect(x-7,y-6,368,58),"",panelStyle);
   if(Button(new Rect(x,y,119,46),"Perspective"))SetCameraView(false);
   if(Button(new Rect(x+127,y,106,46),"Top View"))SetCameraView(true);
   if(Button(new Rect(x+241,y,115,46),showGrid?"Grid On":"Grid Off"))ToggleGrid();
  }
  void DrawLeft(float h){
   float y=96f;
   if(!showLibrary){if(Button(new Rect(12,y,104,42),"Tools >"))showLibrary=true;return;}
   GUI.Box(new Rect(12,y,104,635),"",panelStyle);
   if(Button(new Rect(19,y+7,90,35),"< Tools")){showLibrary=false;return;}
   string[] names={"Select","Walls","Floors","Rooms","Doors","Windows","Stairs","Roofs","Furniture","Materials","Lighting","Terrain","Erase"};
   string[] symbols={"◈","▥","▦","⬡","▯","⊞","≋","⌂","▣","▤","☼","△"};
   float step=45f;
   for(int k=0;k<names.Length;k++){
    bool active=(k==0&&currentTool==Tool.Select)||(k==1&&currentTool==Tool.Wall)||
      (k==2&&currentTool==Tool.Floor)||(k==3&&currentTool==Tool.Room)||
      (k==4&&currentTool==Tool.Door)||(k==5&&currentTool==Tool.Window)||
      (k==6&&currentTool==Tool.Stairs)||(k==7&&currentTool==Tool.Roof)||
      (k==8&&currentTool==Tool.Imported)||(k==9&&currentTool==Tool.Paint)||(k==10&&currentTool==Tool.Light)||(k==11&&currentTool==Tool.Terrain)||(k==12&&currentTool==Tool.Erase);
    float yy=y+47+k*step;
    if(Button(new Rect(19,yy,90,step-5),names[k],active)){
     if(k==0)SelectTool(Tool.Select);
     else if(k==1)SelectTool(Tool.Wall);
     else if(k==2)SelectTool(Tool.Floor);
     else if(k==3)SelectTool(Tool.Room);
     else if(k==4)SelectTool(Tool.Door);
     else if(k==5)SelectTool(Tool.Window);
     else if(k==6)SelectTool(Tool.Stairs);
     else if(k==7)SelectTool(Tool.Roof);
     else if(k==12)SelectTool(Tool.Erase);
     else if(k==9){catalogCategory=8;showCatalog=true;SelectTool(Tool.Paint);}
     else if(k==8){creativeMode=1;catalogCategory=0;showCatalog=true;Notice("Choose a model from My Assets");}
     else if(k==10){SelectTool(Tool.Light);showStudio=true;studioTab=1;}
     else if(k==11){SelectTool(Tool.Terrain);showStudio=true;studioTab=2;}
     else ComingSoon(names[k]);
    }
   }
  }
  void DrawProperties(float w,float h){
   float x=w-351;
   GUI.Box(new Rect(x,96,335,Mathf.Min(h-185f,615f)),"",panelStyle);
   if(Button(new Rect(x+269,105,48,42),"X")){showInspector=false;return;}
   Label(new Rect(x+23,116,282,33),selectedIndex>=0?"Selected Object":"Tool Properties",true);
   string name=selectedIndex>=0&&selectedIndex<project.pieces.Count?project.pieces[selectedIndex].kind:currentTool.ToString();
   Label(new Rect(x+23,155,285,28),name.ToUpperInvariant());
   float y=215;
   if(selectedIndex!=editingPiece){
    editingPiece=selectedIndex;
    if(selectedIndex>=0&&selectedIndex<project.pieces.Count){
     var edit=project.pieces[selectedIndex];
     float[] v={edit.position.x,edit.position.y,edit.position.z,edit.scale.x,edit.scale.y,edit.scale.z};
     for(int k=0;k<6;k++)dimensionDraft[k]=v[k].ToString("F2");
    }
   }
   if(selectedIndex>=0&&selectedIndex<project.pieces.Count){
    var p=project.pieces[selectedIndex];
    if(multiSelection.Count>1){
     Label(new Rect(x+23,183,283,23),multiSelection.Count+" objects selected");
     if(Button(new Rect(x+23,188,137,30),"Group"))GroupSelected();
     if(Button(new Rect(x+169,188,137,30),"Ungroup"))UngroupSelected();
    }
    Label(new Rect(x+23,y,283,25),"POSITION (meters)");y+=27;
    float nextX=FloatField(new Rect(x+23,y,88,40),0,p.position.x);
    float nextY=FloatField(new Rect(x+120,y,88,40),1,p.position.y);
    float nextZ=FloatField(new Rect(x+217,y,88,40),2,p.position.z);
    y+=41;
    Label(new Rect(x+23,y+8,283,25),"SIZE (meters)");y+=40;
    float sx=FloatField(new Rect(x+23,y,88,40),3,p.scale.x);
    float sy=FloatField(new Rect(x+120,y,88,40),4,p.scale.y);
    float sz=FloatField(new Rect(x+217,y,88,40),5,p.scale.z);
    y+=51;
    if(IsRoomFloor(selectedIndex)){
     Label(new Rect(x+23,y,283,27),"ROOM  -  width / depth");
     y+=29;
     if(Button(new Rect(x+23,y,283,42),"Resize Entire Room")){
      ResizeRoom(selectedIndex,new Vector3(nextX,nextY,nextZ),sx,sz);
     }
     y+=50;
    }
    if(Button(new Rect(x+23,y,283,45),"Apply Dimensions")){
     if(IsRoomFloor(selectedIndex)){ResizeRoom(selectedIndex,new Vector3(nextX,nextY,nextZ),sx,sz);return;}
     RememberChange();
     p.position=new Vector3(nextX,nextY,nextZ);
     p.scale=new Vector3(Mathf.Max(.02f,sx),Mathf.Max(.02f,sy),Mathf.Max(.02f,sz));
     if(objects[selectedIndex]!=null)Destroy(objects[selectedIndex]);
     objects[selectedIndex]=Create(p);
     editingPiece=-1;
     RefreshSelectionOutline();
     Notice("Object dimensions updated");
    }
    y+=56;
    if(Button(new Rect(x+23,y,137,43),"Duplicate"))DuplicateSelection();
    if(Button(new Rect(x+169,y,137,43),"Rotate 45°"))RotateSelection();
    y+=51;
    if(p.kind=="imported"||p.kind=="couch"||p.kind=="table"){
     if(Button(new Rect(x+23,y,88,36),"↺ 15°"))RotateSelected(-15f);
     if(Button(new Rect(x+120,y,88,36),"15° ↻"))RotateSelected(15f);
     if(Button(new Rect(x+217,y,88,36),"1.1×"))ResizeSelected(1.1f);
     y+=45;
     if(Button(new Rect(x+23,y,137,36),"Scale −10%"))ResizeSelected(.9f);
     if(Button(new Rect(x+169,y,137,36),furnitureSnap?"Snap: ON":"Snap: OFF"))furnitureSnap=!furnitureSnap;
     y+=45;
    }
    if(p.kind=="light-point"||p.kind=="light-spot"){
    Label(new Rect(x+23,y,282,25),"BRIGHTNESS "+p.lightIntensity.ToString("F2"));y+=26;
    float intensity=GUI.HorizontalSlider(new Rect(x+23,y,282,24),p.lightIntensity,0f,8f);y+=35;
    Label(new Rect(x+23,y,282,25),"RANGE "+p.lightRange.ToString("F1")+" m");y+=26;
    float range=GUI.HorizontalSlider(new Rect(x+23,y,282,24),p.lightRange,1f,30f);y+=35;
    if(Mathf.Abs(intensity-p.lightIntensity)>.001f||Mathf.Abs(range-p.lightRange)>.001f){
     p.lightIntensity=intensity;p.lightRange=range;
     Light component=objects[selectedIndex].GetComponent<Light>();
     if(component!=null){component.intensity=intensity;component.range=range;}
    }
    if(Button(new Rect(x+23,y,283,34),"Light Color: Warm / White")){
     RememberChange();
     p.lightColor=p.lightColor.r>p.lightColor.b+.15f?Color.white:new Color(1f,.78f,.51f);
     Light component=objects[selectedIndex].GetComponent<Light>();
     if(component!=null)component.color=p.lightColor;
    }
    y+=40;
   }
   if(p.kind=="wall"){
     if(Button(new Rect(x+23,y,283,42),"Standard VR Height  ·  2.70 m"))SetStandardWallHeight();
     y+=49;
     if(Button(new Rect(x+23,y,137,42),"Copy Size"))CopyWallSize();
     if(Button(new Rect(x+169,y,137,42),"Paste Size"))PasteWallSize();
     y+=49;
     if(Button(new Rect(x+23,y,283,42),"Build With This Wall"))UseSelectedWallForBuilding();
     y+=49;
    }
    if(Button(new Rect(x+23,y,283,43),"Delete Selected"))DeleteSelection();
   }else{
    Label(new Rect(x+23,y,283,45),"Select a placed object to edit it.");
    y+=45;
    Label(new Rect(x+23,y,282,30),"Building level: "+(buildingLevel+1));y+=29;
    if(Button(new Rect(x+23,y,137,39),"Level -"))buildingLevel=Mathf.Max(0,buildingLevel-1);
    if(Button(new Rect(x+169,y,137,39),"Level +"))buildingLevel=Mathf.Min(8,buildingLevel+1);
    y+=52;
    Label(new Rect(x+23,y,282,30),"Grid snapping: "+gridSize.ToString("F2")+" m");
    y+=28;
    gridSize=GUI.HorizontalSlider(new Rect(x+23,y,282,24),gridSize,.25f,2f);y+=34;
    if(currentTool==Tool.Wall){
     Label(new Rect(x+23,y,282,29),"Wall height: "+wallHeight.ToString("F2")+" m");y+=25;
     wallHeight=GUI.HorizontalSlider(new Rect(x+17,y,232,20),wallHeight,1.5f,5f);y+=39;
     Label(new Rect(x+17,y,220,25),"Thickness: "+wallThickness.ToString("F2")+" m");y+=25;
     wallThickness=GUI.HorizontalSlider(new Rect(x+17,y,232,20),wallThickness,.08f,.5f);
     y+=34;
     if(Button(new Rect(x+23,y,283,40),"Reset Wall Defaults"))ResetWallDefaults();
    }
    if(currentTool==Tool.Imported){
     y+=28;
     Label(new Rect(x+17,y,220,25),"Object scale: "+importedScale.ToString("F2")+"x");y+=29;
     importedScale=GUI.HorizontalSlider(new Rect(x+23,y,282,24),importedScale,.1f,4f);y+=33;
     Label(new Rect(x+17,y,220,25),"Rotation: "+importedRotation.ToString("F0")+"°");y+=27;
     importedRotation=GUI.HorizontalSlider(new Rect(x+17,y,232,18),importedRotation,0f,360f);
    }
   }
   if(selectedIndex>=0)return;
   float bottom=h-345;
   Label(new Rect(x+23,bottom,282,30),"AVATAR SCALE PREVIEW");
   Label(new Rect(x+23,bottom+33,282,30),avatarHeight.ToString("F2")+" m tall");
   avatarHeight=GUI.HorizontalSlider(new Rect(x+23,bottom+73,282,24),avatarHeight,1.2f,2.2f);
  }
  float FloatField(Rect r,int index,float value){
   dimensionDraft[index]=GUI.TextField(r,dimensionDraft[index]??value.ToString("F2"),regularStyle);
   if(float.TryParse(dimensionDraft[index],out float number))return number;
   return value;
  }
  void DrawAssetTile(Rect card,string title,string subtitle,int colorIndex,Action callback,Texture2D previewImage=null){
   GUI.Box(card,"",panelStyle);
   Color[] colors={new Color(.17f,.29f,.36f,.92f),new Color(.28f,.25f,.24f,.95f),
                   new Color(.21f,.30f,.27f,.95f),new Color(.26f,.27f,.34f,.94f)};
   GUI.DrawTexture(new Rect(card.x+6,card.y+6,card.width-12,card.height-65),
     previewImage!=null?previewImage:ThumbnailTexture(colors[colorIndex%colors.Length]),ScaleMode.ScaleToFit);
   string display=title.Length>19?title.Substring(0,16)+"...":title;
   GUI.Label(new Rect(card.x+12,card.y+card.height-54,card.width-22,27),display,smallStyle);
   GUI.Label(new Rect(card.x+12,card.y+card.height-30,card.width-22,25),subtitle,smallStyle);
   if(GUI.Button(card,GUIContent.none,GUIStyle.none))callback();
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
  void DrawCatalog(float w,float h){
   // Give thumbnail labels their own room, including on shorter screens.
   float shelfHeight=252f;
   float top=h-shelfHeight-10f;
   GUI.Box(new Rect(125,top,w-140,shelfHeight),"",panelStyle);
   if(Button(new Rect(w-72,top+63,48,42),"X")){showCatalog=false;return;}
   string[] categories={"My Assets","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor","Materials","Architecture","Other"};
   float x=139f;
   float availableForTabs=Mathf.Max(500f,w-570f);
   float desiredWidths=0f;
   for(int k=0;k<categories.Length;k++)desiredWidths+=(k==0?122f:Mathf.Clamp(categories[k].Length*11f+22f,86f,126f))+7f;
   float tabRatio=Mathf.Min(1f,availableForTabs/desiredWidths);
   for(int k=0;k<categories.Length;k++){
    float width=(k==0?122f:Mathf.Clamp(categories[k].Length*11f+22f,86f,126f))*tabRatio;
    if(Button(new Rect(x,top+13,width,40),categories[k],catalogCategory==k))catalogCategory=k;
    x+=width+7f*tabRatio;
   }
   float right=w-395;
   catalogSearch=GUI.TextField(new Rect(right,top+13,195,40),catalogSearch,regularStyle);
   if(Button(new Rect(w-188,top+13,172,40),"Browse Assets"))ToggleAssetBrowser();
   if(catalogCategory!=8){
    var entries=AssetHub.Catalog();
    var visible=new List<string>();
    foreach(string id in entries){
     string category=catalogCategory==0?"All":categories[catalogCategory];
     if(category!="All"&&AssetHub.Category(id)!=category)continue;
     string keywords=AssetHub.Name(id)+" "+AssetHub.Category(id)+" "+AssetHub.Provider(id);
     if(catalogSearch.Length==0||keywords.IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0)visible.Add(id);
    }
    if(visible.Count==0){
     Label(new Rect(160,top+93,w-480,33),"No models found for this category.",true);
     Label(new Rect(160,top+134,w-480,29),"Supported models appear automatically after download.");
     if(Button(new Rect(w-370,top+101,183,49),"Import Model")){showImport=true;showExport=false;}
    }else{
     float viewport=w-166;
     catalogScroll=GUI.BeginScrollView(new Rect(139,top+68,viewport,166),catalogScroll,
      new Rect(0,0,Mathf.Max(viewport,visible.Count*180),154),false,false);
     for(int k=0;k<visible.Count;k++){
      string id=visible[k];
      string modelName=AssetHub.Name(id);
      if(modelName==id)modelName="Imported Asset "+(k+1);
      DrawAssetTile(new Rect(k*180,0,170,151),modelName,AssetHub.Category(id)+" · "+AssetHub.Provider(id),k,()=>{
       SelectImportedAsset(id);
      },AssetThumbnail(id));
     }
     GUI.EndScrollView();
    }
   }else if(catalogCategory==8){
    Label(new Rect(154,top+59,430,25),"Pick a material, then click a wall, floor or roof.");
    float tiling=GUI.HorizontalSlider(new Rect(w-385,top+69,215,18),selectedTextureTile,.25f,8f);
    selectedTextureTile=Mathf.Round(tiling*4f)/4f;
    Label(new Rect(w-166,top+58,135,30),"Tile: "+selectedTextureTile.ToString("F2")+"x");
    var materials=MaterialLibrary.Catalog();
    float tileWidth=158f;
    int count=FinishNames.Length+materials.Count;
    catalogScroll=GUI.BeginScrollView(new Rect(140,top+100,w-167,140),catalogScroll,
      new Rect(0,0,Mathf.Max(w-175,count*tileWidth),128));
    for(int i=0;i<FinishNames.Length;i++){
     int index=i;float tx=i*tileWidth;
     if(GUI.Button(new Rect(tx,0,150,124),"",GUIStyle.none)){
      selectedTextureId="";selectedFinish=index;SelectTool(Tool.Paint);
     }
     GUI.DrawTexture(new Rect(tx+4,4,142,76),ThumbnailTexture(FinishColors[i]));
     GUI.Label(new Rect(tx+7,84,142,29),FinishNames[i],smallStyle);
     if(string.IsNullOrEmpty(selectedTextureId)&&selectedFinish==i)
      GUI.Box(new Rect(tx+3,115,143,5),"",activeStyle);
    }
    for(int i=0;i<materials.Count;i++){
     var mat=materials[i];float tx=(i+FinishNames.Length)*tileWidth;
     if(GUI.Button(new Rect(tx,0,150,124),"",GUIStyle.none)){
      selectedTextureId=mat.id;SelectTool(Tool.Paint);
     }
     Texture2D pic=GetMaterialImage(mat.id);
     if(pic!=null)GUI.DrawTexture(new Rect(tx+4,4,142,76),pic,ScaleMode.ScaleAndCrop);
     GUI.Label(new Rect(tx+7,84,140,25),mat.name.Length>18?mat.name.Substring(0,15)+"...":mat.name,smallStyle);
     GUI.Label(new Rect(tx+7,105,140,18),mat.provider,smallStyle);
     if(selectedTextureId==mat.id)GUI.Box(new Rect(tx+3,119,143,5),"",activeStyle);
    }
    GUI.EndScrollView();
   }
  }
  void ExitApplication(bool saveFirst){
   if(quitting)return;
   if(saveFirst&&!Save()){Notice("Save failed; exit cancelled");return;}
   quitting=true;showConfirmExit=false;showPause=false;
   if(browserProcess!=null&&!browserProcess.HasExited)browserProcess.Kill();
   Application.Quit();
  }
  void DrawPause(float w,float h){
   GUI.Box(new Rect(0,0,w,h),"",panelStyle);
   float x=w*.5f-182,y=h*.5f-285;
   if(showConfirmExit){
    GUI.Box(new Rect(x-30,y+155,424,274),"",panelStyle);
    Label(new Rect(x+3,y+178,355,35),"Exit WorldBuilder?",true);
    Label(new Rect(x+3,y+213,362,43),"Save your work before leaving.");
    if(Button(new Rect(x+5,y+259,350,38),"Save and Exit"))ExitApplication(true);
    if(Button(new Rect(x+5,y+304,350,38),"Exit Without Saving"))ExitApplication(false);
    if(Button(new Rect(x+5,y+349,350,38),"Cancel"))showConfirmExit=false;
    return;
   }
   GUI.Box(new Rect(x,y,364,570),"",panelStyle);
   Label(new Rect(x+37,y+23,295,36),"WorldBuilder",true);
   if(Button(new Rect(x+30,y+73,304,43),"Resume Building"))showPause=false;
   if(Button(new Rect(x+30,y+122,304,43),"Save Project"))Save();
   if(Button(new Rect(x+30,y+171,304,43),"Walk Preview"))EnterWalk();
   if(Button(new Rect(x+30,y+220,304,43),"Export to VRChat")){showPause=false;showExport=true;}
   if(Button(new Rect(x+30,y+269,304,43),"Projects")){showPause=false;showProjects=true;}
   if(Button(new Rect(x+30,y+318,304,43),"Settings & Help")){showPause=false;showSettings=true;}
   if(Button(new Rect(x+30,y+367,304,43),"New Project")){showPause=false;NewProject();}
   if(Button(new Rect(x+30,y+433,304,49),"Exit WorldBuilder")){showConfirmExit=true;}
   Label(new Rect(x+30,y+507,304,29),"Esc to return to your world");
  }
  void DrawSettings(float w,float h){
   float x=w*.5f-250,y=h*.5f-238;
   GUI.Box(new Rect(x,y,500,476),"",panelStyle);
   Label(new Rect(x+25,y+18,455,35),"Settings & Help",true);
   Label(new Rect(x+25,y+64,455,28),"BUILD CONTROLS");
   Label(new Rect(x+25,y+100,455,27),"1 Walls   2 Floors   3 Sofa   4 Table   5 Erase");
   Label(new Rect(x+25,y+133,455,27),"0 Select   6 Rooms   M Materials   G Grid");
   Label(new Rect(x+25,y+185,455,27),"Drag object   R Rotate   Ctrl+D Duplicate   Del Remove");
   Label(new Rect(x+25,y+167,455,27),"Ctrl+S Save   Ctrl+O Reload   Esc Menu   T/P Views");
   Label(new Rect(x+25,y+206,455,26),"CAMERA");
   Label(new Rect(x+25,y+239,455,28),"Right mouse: orbit    Scroll: zoom");
   Label(new Rect(x+25,y+271,455,28),"Walk Preview: WASD, mouse look, Esc to return");
   Label(new Rect(x+25,y+322,455,27),"WorldBuilder is currently in development.");
   if(Button(new Rect(x+24,y+397,452,42),"Back to Building"))showSettings=false;
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
     "Supported 3D: OBJ, GLB, glTF, ZIP   ·   Textures-only packages do not appear under My Assets.");
  }
  void DrawRecoveryPrompt(float w,float h){
   GUI.Box(new Rect(0,0,w,h),"",panelStyle);
   float width=Mathf.Min(660f,w-48f);
   float x=(w-width)*.5f,y=Mathf.Max(30f,(h-390f)*.5f);
   GUI.Box(new Rect(x,y,width,390f),"",panelStyle);
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
    Notice("Saved project opened. Recovery is still available in Projects.");
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
   if(showCatalog)DrawCatalog(w,h);
   else {
    GUI.Box(new Rect(127,h-62,344,54),"",panelStyle);
    if(Button(new Rect(134,h-56,162,42),"Open Catalog"))showCatalog=true;
    if(Button(new Rect(302,h-56,162,42),"Import Model")){showImport=true;showExport=false;}
   }
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
   if(Time.time<statusUntil&&!showPause&&!showSettings&&!showHelp){
    // Notifications float above, never across the asset shelf or its tabs.
    float noticeWidth=Mathf.Min(560f,w-550f);
    float toastY=showCatalog?h-314f:h-113f;
    GUI.Box(new Rect(140,toastY,noticeWidth,39),"",panelStyle);
    GUI.Label(new Rect(156,toastY+6,noticeWidth-30,28),status,captionStyle);
   }
  }
 }
}
