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
  public Piece(string kind, Vector3 position, Vector3 scale, Quaternion rotation) {
   this.kind=kind;this.position=position;this.scale=scale;this.rotation=rotation;
  }
 }
 [Serializable] public class Project {
  public int schema=2;
  public List<Piece> pieces=new List<Piece>();
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
  public enum Tool { Wall, Floor, Couch, Table, Erase, Imported, Select, Room, Door, Window, Roof, Stairs }
  public Tool currentTool=Tool.Wall;
  private Vector3? anchor;
  private readonly Stack<string> undoHistory=new Stack<string>();
  private readonly Stack<string> redoHistory=new Stack<string>();
  private bool restoringHistory=false;
  private Project project=new Project();
  private readonly List<GameObject> objects=new List<GameObject>();
  private string filePath;
  private bool showHelp=false;
  private bool showPause=false;
  private bool showConfirmExit=false;
  private bool showSettings=false;
  private int selectedIndex=-1;
  private bool draggingSelection=false;
  private Vector3 selectionDragOffset;
  private LineRenderer selectionOutline;
  private int creativeMode=0;
  private int catalogCategory=0;
  private Vector2 catalogScroll;
  private string catalogSearch="";
  private int editingPiece=-1;
  private readonly string[] dimensionDraft=new string[6];
  private string status="Ready to build";
  private float statusUntil;
  private bool showLibrary=true;
  private bool showCatalog=false;
  private bool showInspector=false;
  private bool showProjects=false;
  private GameObject preview;
  private string importedId;
  private float importedScale=1f;
  private float importedRotation=0f;
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
  private string assetStatus="Drop ZIP/OBJ files into a provider Downloads folder";
  private Vector2 assetScroll;
  private bool showExport=false;
  private bool showGrid=true;
  private readonly List<GameObject> gridMarkers=new List<GameObject>();
  bool OverUI(){
   if(walkthrough!=null&&walkthrough.IsWalking)return true;
   float scale=UIScale;
   float w=Screen.width/scale,h=Screen.height/scale;
   Vector2 p=new Vector2(Input.mousePosition.x/scale,(Screen.height-Input.mousePosition.y)/scale);
   return p.y<88f||(showCatalog&&p.y>h-272f)||
     (!showCatalog&&p.y>h-70f&&p.x<480f)||
     (showLibrary&&p.x<122f&&p.y>=94f&&p.y<735f)||
     (showInspector&&p.x>w-359f&&p.y>=94f&&p.y<h-175f)||
     (p.x>125f&&p.x<515f&&p.y<159f)||
     showPause||showConfirmExit||showSettings||showHelp||
     (showProjects&&p.x>w*.5f-200f&&p.x<w*.5f+210f&&p.y<505f)||
     ((showImport||showExport)&&p.x>w*.5f-275f&&p.x<w*.5f+275f&&p.y<335f)||
     showAssets;
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
  void OnApplicationQuit(){
   if(browserProcess!=null&&!browserProcess.HasExited)browserProcess.Kill();
  }
  void SelectObject(int index){
   selectedIndex=index;
   if(index>=0)showInspector=true;
   editingPiece=-1;
   RefreshSelectionOutline();
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
   Add(copy);
   SelectObject(project.pieces.Count-1);
   Notice("Object duplicated. Drag to reposition.");
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
  void UpdatePreview(){
   if(OverUI()||currentTool==Tool.Erase||!MouseOnGround(out Vector3 p)){
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
   preview.SetActive(true);
   preview.transform.rotation=Quaternion.identity;
   if(currentTool==Tool.Imported){
    preview.transform.position=p+Vector3.up*.5f;
    preview.transform.localScale=Vector3.one*importedScale;
    preview.transform.rotation=Quaternion.Euler(0,importedRotation,0);
   }else if(currentTool==Tool.Door||currentTool==Tool.Window){
    preview.transform.position=p+Vector3.up*(currentTool==Tool.Window?1.4f:1f);
    preview.transform.localScale=new Vector3(currentTool==Tool.Window?1.2f:.9f,currentTool==Tool.Window?1.15f:2f,.08f);
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
  void SelectTool(Tool tool){currentTool=tool;anchor=null;Notice(tool+" tool selected");}
  string AssetLibrary(){string d=Path.Combine(Application.persistentDataPath,"ImportedAssets");Directory.CreateDirectory(d);return d;}
  void BrowseObj(){
   try{
    var psi=new System.Diagnostics.ProcessStartInfo();
    psi.FileName="powershell.exe";
    psi.Arguments="-NoProfile -STA -Command \"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Filter='Wavefront OBJ (*.obj)|*.obj'; $d.CheckFileExists=$true; if($d.ShowDialog() -eq 'OK') { [Console]::Write($d.FileName) }\"";
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
    if(!File.Exists(path))throw new FileNotFoundException("File not found. Choose an existing OBJ file, not a folder.");
    if(!string.Equals(Path.GetExtension(path),".obj",StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("This file is "+Path.GetExtension(path)+", not .obj.");
    string id=Guid.NewGuid().ToString("N");
    string directory=Path.Combine(AssetLibrary(),id);
    Directory.CreateDirectory(directory);
    foreach(string source in Directory.GetFiles(Path.GetDirectoryName(path))){
     string ext=Path.GetExtension(source).ToLowerInvariant();
     if(ext==".mtl"||ext==".png"||ext==".jpg"||ext==".jpeg")File.Copy(source,Path.Combine(directory,Path.GetFileName(source)),true);
    }
    string model=Path.Combine(directory,"model.obj");
    File.Copy(path,model,true);
    GameObject test=ImportedObj.Load(model);
    Destroy(test);
    importedId=id;
    importError="";
    currentTool=Tool.Imported;
    showImport=false;
    Notice("OBJ imported. Click ground to place.");
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
      "Geometry, box colliders and basic colors are preserved. Third-party assets are not yet supported.\\r\\n");
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
  void NewProject(){RememberChange();SelectObject(-1);project=new Project();foreach(var obj in objects)if(obj!=null)Destroy(obj);objects.Clear();anchor=null;Notice("New empty project. Save to keep it.");}
  string ProjectsFolder(){string p=Path.Combine(Application.persistentDataPath,"Projects");Directory.CreateDirectory(p);return p;}
  void SaveAsNamed(string name){
   try{string safe=string.Join("_",name.Split(Path.GetInvalidFileNameChars()));if(string.IsNullOrWhiteSpace(safe))safe="My World";
    filePath=Path.Combine(ProjectsFolder(),safe+".json");Save();}
   catch(Exception e){Notice("Save failed: "+e.Message);}
  }
  void OpenProject(string path){filePath=path;Load();showProjects=false;}

  void Start() {
   if(sceneCamera==null)sceneCamera=Camera.main;
   if(sceneCamera!=null)walkthrough=sceneCamera.GetComponent<WalkthroughCamera>();
   targetVrcProject=PlayerPrefs.GetString("WorldBuilder.VRChatProject","");
   AssetHub.Initialize();
   // Asset websites now render through the embedded WebView2 browser.
   filePath=Path.Combine(Application.persistentDataPath,"WorldBuilder-project.json");
   if(File.Exists(filePath))Load();
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
  void Update() {
   if(walkthrough!=null&&walkthrough.IsWalking)return;
   if(showAssets&&(browserProcess==null||browserProcess.HasExited)){showAssets=false;if(browserProcess!=null){browserProcess.Dispose();browserProcess=null;}}
   if(Time.unscaledTime>=nextAssetScan){
    nextAssetScan=Time.unscaledTime+6f;
    string msg;
    int count=AssetHub.Scan(out msg);
    if(msg.Length>0){assetStatus=msg;if(count>0)Notice(msg);}
   }
   if(Input.GetKeyDown(KeyCode.Alpha0))SelectTool(Tool.Select);
   if(Input.GetKeyDown(KeyCode.T))SetCameraView(true);
   if(Input.GetKeyDown(KeyCode.P))SetCameraView(false);
   if(Input.GetKeyDown(KeyCode.Alpha1))SelectTool(Tool.Wall);
   if(Input.GetKeyDown(KeyCode.Alpha2))SelectTool(Tool.Floor);
   if(Input.GetKeyDown(KeyCode.Alpha3))SelectTool(Tool.Couch);
   if(Input.GetKeyDown(KeyCode.Alpha4))SelectTool(Tool.Table);
   if(Input.GetKeyDown(KeyCode.Alpha5))SelectTool(Tool.Erase);
   if(Input.GetKeyDown(KeyCode.Alpha6))SelectTool(Tool.Room);
   if(Input.GetKeyDown(KeyCode.Escape)){
    if(showConfirmExit)showConfirmExit=false;
    else if(showImport||showExport||showProjects||showSettings||showHelp){
     showImport=false;showExport=false;showProjects=false;showSettings=false;showHelp=false;
    }else{showPause=!showPause;anchor=null;}
   }
   if(Input.GetKeyDown(KeyCode.F1)){showHelp=!showHelp;showPause=false;}
   if(Input.GetKeyDown(KeyCode.G))ToggleGrid();
   if(!showPause&&!showConfirmExit&&!showSettings&&!showExport&&!showImport&&!showProjects){
    if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.D))DuplicateSelection();
    if(Input.GetKeyDown(KeyCode.R)&&currentTool==Tool.Select)RotateSelection();
    if(Input.GetKeyDown(KeyCode.Delete)&&currentTool==Tool.Select)DeleteSelection();
    if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Y))RedoLast();
   }
   if(draggingSelection){
    if(Input.GetMouseButtonUp(0)){draggingSelection=false;Notice("Object moved");}
    else if(selectedIndex>=0&&selectedIndex<project.pieces.Count&&!OverUI()&&MouseOnGround(out Vector3 dragPoint)){
     var piece=project.pieces[selectedIndex];
     Vector3 target=dragPoint+selectionDragOffset;
     target.y=piece.position.y;
     piece.position=target;
     objects[selectedIndex].transform.position=target;
     RefreshSelectionOutline();
    }
   }
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))UndoLast();
   UpdatePreview();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.S))Save();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.O))Load();
   if(!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftAlt) || Input.GetMouseButton(1))return;
   if(OverUI()||!MouseOnGround(out Vector3 p))return;
   if(currentTool==Tool.Select){
    Ray pick=sceneCamera.ScreenPointToRay(Input.mousePosition);
    if(Physics.Raycast(pick,out RaycastHit hit,500f)){
     int hitIndex=-1;
     for(int j=0;j<objects.Count;j++){
      if(objects[j]!=null&&(objects[j]==hit.collider.gameObject||hit.collider.transform.IsChildOf(objects[j].transform))){hitIndex=j;break;}
     }
     SelectObject(hitIndex);
     if(hitIndex>=0){
      RememberChange();
      draggingSelection=true;
      selectionDragOffset=project.pieces[hitIndex].position-p;
     }
    }else SelectObject(-1);
    return;
   }
   if(currentTool==Tool.Erase) { Erase(p);return; }
   if(currentTool==Tool.Door||currentTool==Tool.Window){AddOpening(currentTool==Tool.Window);return;}
   if(currentTool==Tool.Imported){
    if(string.IsNullOrEmpty(importedId)){Notice("Import an OBJ first");return;}
    var item=new Piece("imported",p,Vector3.one*importedScale,Quaternion.Euler(0,importedRotation,0));
    item.assetId=importedId;Add(item);return;
   }
   if(currentTool==Tool.Couch||currentTool==Tool.Table) {
    Add(new Piece(currentTool==Tool.Couch?"couch":"table",p+Vector3.up*.4f,new Vector3(currentTool==Tool.Couch?2f:1.2f,.8f,currentTool==Tool.Couch?.9f:1.2f),Quaternion.identity));return;
   }
   if(!anchor.HasValue){anchor=p;return;}
   Vector3 start=anchor.Value;anchor=null;
   if(Vector3.Distance(start,p)<.01f)return;
   if(currentTool==Tool.Room){MakeRoom(start,p);return;}
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
   float oldLevel=buildingLevel;
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
   buildingLevel=(int)oldLevel;
   Notice("Room resized to "+newWidth.ToString("F2")+" × "+newDepth.ToString("F2")+" m");
  }
  void MakeRoof(Vector3 a,Vector3 b){
   float width=Mathf.Abs(b.x-a.x),depth=Mathf.Abs(b.z-a.z);
   if(width<.25f||depth<.25f){Notice("Roof needs width and depth");return;}
   Add(new Piece("roof",(a+b)*.5f+Vector3.up*((buildingLevel+1)*LevelHeight+.12f),
     new Vector3(width+.4f,.24f,depth+.4f),Quaternion.identity));
   Notice("Flat roof created");
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
  void AddOpening(bool window){
   if(sceneCamera==null)return;
   Ray ray=sceneCamera.ScreenPointToRay(Input.mousePosition);
   if(!Physics.Raycast(ray,out RaycastHit hit,300f)){Notice("Click an existing wall");return;}
   int idx=-1;
   for(int i=0;i<objects.Count;i++){
    if(project.pieces[i].kind=="wall"&&objects[i]!=null&&
       (hit.collider.gameObject==objects[i]||hit.collider.transform.IsChildOf(objects[i].transform))){idx=i;break;}
   }
   if(idx<0){Notice("Click an existing wall to add an opening");return;}
   Piece original=project.pieces[idx];
   float length=original.scale.x,height=original.scale.y;
   float width=window?1.2f:.9f;
   if(length<width+.3f||height<1.6f){Notice("Wall is too small for this opening");return;}
   Vector3 local=Quaternion.Inverse(original.rotation)*(hit.point-original.position);
   float center=Mathf.Clamp(local.x,-length*.5f+width*.5f+.08f,length*.5f-width*.5f-.08f);
   float left=center-width*.5f,right=center+width*.5f;
   float bottom=window?.85f:0f,openingHeight=window?Mathf.Min(1.15f,height-1.1f):Mathf.Min(2.1f,height-.15f);
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
    AddWithoutHistory(section);
   };
   part(-length*.5f,left,0,height,"wall");
   part(right,length*.5f,0,height,"wall");
   part(left,right,bottom+openingHeight,height,"wall");
   if(window)part(left,right,0,bottom,"wall");
   // The gap remains an actual opening with colliders on surrounding pieces.
   Notice(window?"Window opening created":"Door opening created");
  }
  GameObject Create(Piece piece){
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
   if(material!=null)obj.GetComponent<Renderer>().sharedMaterial=material;
   else obj.GetComponent<Renderer>().material.color=piece.kind=="wall"?new Color(.89f,.91f,.94f):piece.kind=="floor"?new Color(.47f,.52f,.58f):new Color(.25f,.6f,.7f);
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
   try{File.WriteAllText(filePath,JsonUtility.ToJson(project,true));Debug.Log("Saved "+filePath);Notice("Saved: "+Path.GetFileName(filePath));return true;}
   catch(Exception e){Debug.LogError(e);Notice("Save failed: "+e.Message);return false;}
  }
  public void Load(){
   try{
    var loaded=JsonUtility.FromJson<Project>(File.ReadAllText(filePath));
    if(loaded==null||loaded.schema!=2||loaded.pieces==null)throw new InvalidDataException("Unsupported project schema");
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();SelectObject(-1);project=loaded;
    foreach(var piece in project.pieces)objects.Add(Create(piece));Notice("Loaded: "+Path.GetFileName(filePath));
   }catch(Exception e){Debug.LogError("Load failed: "+e.Message);Notice("Load failed: "+e.Message);}
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
     else {creativeMode=n;if(n>0)ComingSoon(modes[n]+" tools");}
    }
   }
   if(Button(new Rect(w-330,22,64,46),"Undo"))UndoLast();
   if(Button(new Rect(w-258,22,70,46),"Save"))Save();
   if(Button(new Rect(w-180,22,77,46),"Panels"))showInspector=!showInspector;
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
   GUI.Box(new Rect(12,y,104,602),"",panelStyle);
   if(Button(new Rect(19,y+7,90,35),"< Tools")){showLibrary=false;return;}
   string[] names={"Select","Walls","Floors","Rooms","Doors","Windows","Stairs","Roofs","Furniture","Materials","Lighting","Terrain"};
   string[] symbols={"◈","▥","▦","⬡","▯","⊞","≋","⌂","▣","▤","☼","△"};
   float step=45f;
   for(int k=0;k<names.Length;k++){
    bool active=(k==0&&currentTool==Tool.Select)||(k==1&&currentTool==Tool.Wall)||
      (k==2&&currentTool==Tool.Floor)||(k==3&&currentTool==Tool.Room)||
      (k==4&&currentTool==Tool.Door)||(k==5&&currentTool==Tool.Window)||
      (k==6&&currentTool==Tool.Stairs)||(k==7&&currentTool==Tool.Roof)||
      (k==8&&currentTool==Tool.Imported);
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
     else if(k==8){creativeMode=1;catalogCategory=0;showCatalog=true;Notice("Choose a model from My Assets");}
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
    if(Button(new Rect(x+23,y,283,45),"Apply Dimensions")){
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
    }
    if(currentTool==Tool.Imported){
     y+=28;
     Label(new Rect(x+17,y,220,25),"Object scale: "+importedScale.ToString("F2")+"x");y+=29;
     importedScale=GUI.HorizontalSlider(new Rect(x+23,y,282,24),importedScale,.1f,4f);y+=33;
     Label(new Rect(x+17,y,220,25),"Rotation: "+importedRotation.ToString("F0")+"°");y+=27;
     importedRotation=GUI.HorizontalSlider(new Rect(x+17,y,232,18),importedRotation,0f,360f);
    }
   }
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
  void DrawAssetTile(Rect card,string title,string subtitle,int colorIndex,Action callback){
   GUI.Box(card,"",panelStyle);
   Color[] colors={new Color(.17f,.29f,.36f,.92f),new Color(.28f,.25f,.24f,.95f),
                   new Color(.21f,.30f,.27f,.95f),new Color(.26f,.27f,.34f,.94f)};
   GUI.DrawTexture(new Rect(card.x+6,card.y+6,card.width-12,card.height-65),
     ThumbnailTexture(colors[colorIndex%colors.Length]),ScaleMode.StretchToFill);
   string display=title.Length>19?title.Substring(0,16)+"...":title;
   GUI.Label(new Rect(card.x+12,card.y+card.height-54,card.width-22,27),display,smallStyle);
   GUI.Label(new Rect(card.x+12,card.y+card.height-30,card.width-22,25),subtitle,smallStyle);
   if(GUI.Button(card,GUIContent.none,GUIStyle.none))callback();
  }
  private readonly Dictionary<int,Texture2D> catalogBackgrounds=new Dictionary<int,Texture2D>();
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
   string[] categories={"My Assets","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor","Materials"};
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
   if(catalogCategory==0){
    var entries=AssetHub.Catalog();
    var visible=new List<string>();
    foreach(string id in entries){
     if(catalogSearch.Length==0||AssetHub.Name(id).IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0)visible.Add(id);
    }
    if(visible.Count==0){
     Label(new Rect(160,top+93,w-480,33),"Your imported assets will appear here.",true);
     Label(new Rect(160,top+134,w-480,29),"Use Browse Assets to download models, or Import OBJ to add local models.");
     if(Button(new Rect(w-370,top+101,183,49),"Import OBJ")){showImport=true;showExport=false;}
    }else{
     float viewport=w-166;
     catalogScroll=GUI.BeginScrollView(new Rect(139,top+68,viewport,166),catalogScroll,
      new Rect(0,0,Mathf.Max(viewport,visible.Count*180),154),false,false);
     for(int k=0;k<visible.Count;k++){
      string id=visible[k];
      string modelName=AssetHub.Name(id);
      if(modelName==id)modelName="Imported Asset "+(k+1);
      DrawAssetTile(new Rect(k*180,0,170,151),modelName,"Imported model",k,()=>{
       importedId=id;SelectTool(Tool.Imported);
       Notice("Click the ground to place "+AssetHub.Name(id));
      });
     }
     GUI.EndScrollView();
    }
   }else if(catalogCategory==1){
    DrawAssetTile(new Rect(144,top+70,170,151),"Sofa","Basic placeholder",0,()=>SelectTool(Tool.Couch));
    DrawAssetTile(new Rect(324,top+70,170,151),"Table","Basic placeholder",1,()=>SelectTool(Tool.Table));
    Label(new Rect(530,top+116,520,32),"Browse Assets to expand your furniture catalog.");
   }else{
    Label(new Rect(165,top+103,w-400,34),"This collection is coming soon.",true);
   }
  }
  void DrawPause(float w,float h){
   GUI.Box(new Rect(0,0,w,h),"",panelStyle);
   float x=w*.5f-182,y=h*.5f-285;
   GUI.Box(new Rect(x,y,364,570),"",panelStyle);
   Label(new Rect(x+37,y+23,295,36),"WorldBuilder",true);
   if(Button(new Rect(x+30,y+73,304,43),"Resume Building"))showPause=false;
   if(Button(new Rect(x+30,y+122,304,43),"Save Project"))Save();
   if(Button(new Rect(x+30,y+171,304,43),"Walk Preview"))EnterWalk();
   if(Button(new Rect(x+30,y+220,304,43),"Export to VRChat")){showPause=false;showExport=true;}
   if(Button(new Rect(x+30,y+269,304,43),"Projects")){showPause=false;showProjects=true;}
   if(Button(new Rect(x+30,y+318,304,43),"Settings & Help")){showPause=false;showSettings=true;}
   if(Button(new Rect(x+30,y+367,304,43),"New Project")){showPause=false;NewProject();}
   if(Button(new Rect(x+30,y+433,304,49),"Exit WorldBuilder"))showConfirmExit=true;
   Label(new Rect(x+30,y+507,304,29),"Esc to return to your world");
   if(showConfirmExit){
    GUI.Box(new Rect(x-30,y+155,424,274),"",panelStyle);
    Label(new Rect(x+3,y+178,355,35),"Exit WorldBuilder?",true);
    Label(new Rect(x+3,y+213,362,43),"Save your work before leaving.");
    if(Button(new Rect(x+5,y+259,350,38),"Save and Exit")){if(Save())Application.Quit();}
    if(Button(new Rect(x+5,y+304,350,38),"Exit Without Saving"))Application.Quit();
    if(Button(new Rect(x+5,y+349,350,38),"Cancel"))showConfirmExit=false;
   }
  }
  void DrawSettings(float w,float h){
   float x=w*.5f-250,y=h*.5f-238;
   GUI.Box(new Rect(x,y,500,476),"",panelStyle);
   Label(new Rect(x+25,y+18,455,35),"Settings & Help",true);
   Label(new Rect(x+25,y+64,455,28),"BUILD CONTROLS");
   Label(new Rect(x+25,y+100,455,27),"1 Walls   2 Floors   3 Sofa   4 Table   5 Erase");
   Label(new Rect(x+25,y+133,455,27),"0 Select   G Grid   Ctrl+Z Undo");
   Label(new Rect(x+25,y+185,455,27),"Drag object to move   R Rotate   Ctrl+D Duplicate");
   Label(new Rect(x+25,y+167,455,27),"Ctrl+S Save   Ctrl+O Reload   Esc Menu");
   Label(new Rect(x+25,y+206,455,26),"CAMERA");
   Label(new Rect(x+25,y+239,455,28),"Right mouse: orbit    Scroll: zoom");
   Label(new Rect(x+25,y+271,455,28),"Walk Preview: WASD, mouse look, Esc to return");
   Label(new Rect(x+25,y+322,455,27),"WorldBuilder is currently in development.");
   if(Button(new Rect(x+24,y+397,452,42),"Back to Building"))showSettings=false;
  }
  void OnGUI(){
   InitStyles();
   GUI.matrix=Matrix4x4.Scale(new Vector3(UIScale,UIScale,1f));
   float w=Screen.width/UIScale,h=Screen.height/UIScale;
   if(walkthrough!=null&&walkthrough.IsWalking){
    GUI.Box(new Rect(0,0,w,48),"",panelStyle);
    Label(new Rect(19,10,Mathf.Min(w-35,840),30),"LIVE PREVIEW     WASD Move   |   Mouse Look   |   Esc Return");
    return;
   }
   DrawToolbar(w);
   DrawLeft(h);
   DrawViewportControls(h);
   if(showInspector)DrawProperties(w,h);
   if(showCatalog)DrawCatalog(w,h);
   else {
    GUI.Box(new Rect(127,h-62,344,54),"",panelStyle);
    if(Button(new Rect(134,h-56,162,42),"Open Catalog"))showCatalog=true;
    if(Button(new Rect(302,h-56,162,42),"Import OBJ")){showImport=true;showExport=false;}
   }
   if(showImport){
    float x=w*.5f-265;
    GUI.Box(new Rect(x,75,530,255),"",panelStyle);
    Label(new Rect(x+20,90,480,32),"Import OBJ Model",true);
    Label(new Rect(x+20,126,490,23),"Choose an OBJ file. Keep textures and MTL beside it.");
    importPath=GUI.TextField(new Rect(x+20,158,350,30),importPath,regularStyle);
    if(Button(new Rect(x+380,158,130,30),"Browse Files"))BrowseObj();
    if(Button(new Rect(x+20,206,235,36),"Import & Select"))ImportObj();
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
    GUI.Box(new Rect(x,77,370,400),"",panelStyle);
    Label(new Rect(x+20,93,330,35),"Projects",true);
    if(Button(new Rect(x+15,131,340,34),"Reload last saved project"))Load();
    string[] paths=Directory.GetFiles(ProjectsFolder(),"*.json");
    Array.Sort(paths);Array.Reverse(paths);
    for(int k=0;k<Mathf.Min(paths.Length,5);k++){
     string path=paths[k];
     if(Button(new Rect(x+15,176+k*39,340,34),Path.GetFileNameWithoutExtension(path)))OpenProject(path);
    }
    if(Button(new Rect(x+15,367,340,29),"Close"))showProjects=false;
   }
   if(showSettings||showHelp)DrawSettings(w,h);
   if(showPause)DrawPause(w,h);
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
