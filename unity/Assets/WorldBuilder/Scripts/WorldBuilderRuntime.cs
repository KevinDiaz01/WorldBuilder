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
  public enum Tool { Wall, Floor, Couch, Table, Erase, Imported, Select }
  public Tool currentTool=Tool.Wall;
  private Vector3? anchor;
  private Project project=new Project();
  private readonly List<GameObject> objects=new List<GameObject>();
  private string filePath;
  private bool showHelp=false;
  private bool showPause=false;
  private bool showConfirmExit=false;
  private bool showSettings=false;
  private int selectedIndex=-1;
  private int creativeMode=0;
  private int catalogCategory=0;
  private Vector2 catalogScroll;
  private string catalogSearch="";
  private int editingPiece=-1;
  private readonly string[] dimensionDraft=new string[6];
  private string status="Ready to build";
  private float statusUntil;
  private bool showLibrary=true;
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
   Vector2 p=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
   return p.y<66f||p.y>Screen.height-54f||
     (showLibrary&&p.x<100f)||
     (p.x>Screen.width-290f&&p.y<Screen.height-175f)||
     (p.y>Screen.height-175f)||
     showPause||showConfirmExit||showSettings||
     (showProjects&&p.x>Screen.width-365f&&p.y<520f)||

     ((showImport||showExport)&&p.x>Screen.width*.5f-265&&p.x<Screen.width*.5f+265&&p.y<330)||
     (showAssets&&p.x>Screen.width*.5f-360&&p.x<Screen.width*.5f+360&&p.y<Screen.height-65);
  }
  void ToggleAssetBrowser(){
   try{
    if(browserProcess!=null&&!browserProcess.HasExited){
     browserProcess.Kill();browserProcess.Dispose();browserProcess=null;
     showAssets=false;Notice("Assets browser closed");return;
    }
    string exe=Path.Combine(Path.GetDirectoryName(Application.dataPath),"WorldBuilderAssetBrowser.exe");
    if(!File.Exists(exe)){Notice("Asset browser component is missing. Reinstall the complete update.");return;}
    IntPtr hwnd=System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    if(hwnd==IntPtr.Zero){Notice("Unable to locate WorldBuilder window");return;}
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
  void UndoLast(){
   selectedIndex=-1;
   int i=objects.Count-1;
   if(i<0){Notice("Nothing to undo");return;}
   if(objects[i]!=null)Destroy(objects[i]);
   objects.RemoveAt(i);project.pieces.RemoveAt(i);anchor=null;
   Notice("Last object removed");
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
   }else if(currentTool==Tool.Couch||currentTool==Tool.Table){
    bool sofa=currentTool==Tool.Couch;
    preview.transform.position=p+Vector3.up*.4f;
    preview.transform.localScale=new Vector3(sofa?2f:1.2f,.8f,sofa?.9f:1.2f);
   }else if(anchor.HasValue){
    Vector3 d=p-anchor.Value;
    if(currentTool==Tool.Wall){
     preview.transform.position=(p+anchor.Value)*.5f+Vector3.up*wallHeight*.5f;
     preview.transform.localScale=new Vector3(Mathf.Max(.05f,d.magnitude),wallHeight,wallThickness);
     preview.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg,0);
    }else{
     preview.transform.position=(p+anchor.Value)*.5f+Vector3.up*FloorCenter;
     preview.transform.localScale=new Vector3(Mathf.Max(gridSize,Mathf.Abs(d.x)),FloorThickness,Mathf.Max(gridSize,Mathf.Abs(d.z)));
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
  void NewProject(){selectedIndex=-1;project=new Project();foreach(var obj in objects)if(obj!=null)Destroy(obj);objects.Clear();anchor=null;Notice("New empty project. Save to keep it.");}
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
   if(Time.unscaledTime>=nextAssetScan){
    nextAssetScan=Time.unscaledTime+6f;
    string msg;
    int count=AssetHub.Scan(out msg);
    if(msg.Length>0){assetStatus=msg;if(count>0)Notice(msg);}
   }
   if(Input.GetKeyDown(KeyCode.Alpha0))SelectTool(Tool.Select);
   if(Input.GetKeyDown(KeyCode.Alpha1))SelectTool(Tool.Wall);
   if(Input.GetKeyDown(KeyCode.Alpha2))SelectTool(Tool.Floor);
   if(Input.GetKeyDown(KeyCode.Alpha3))SelectTool(Tool.Couch);
   if(Input.GetKeyDown(KeyCode.Alpha4))SelectTool(Tool.Table);
   if(Input.GetKeyDown(KeyCode.Alpha5))SelectTool(Tool.Erase);
   if(Input.GetKeyDown(KeyCode.Escape)){
    if(showConfirmExit)showConfirmExit=false;
    else if(showImport||showExport||showProjects||showSettings||showHelp){
     showImport=false;showExport=false;showProjects=false;showSettings=false;showHelp=false;
    }else{showPause=!showPause;anchor=null;}
   }
   if(Input.GetKeyDown(KeyCode.F1)){showHelp=!showHelp;showPause=false;}
   if(Input.GetKeyDown(KeyCode.G))ToggleGrid();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))UndoLast();
   UpdatePreview();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.S))Save();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.O))Load();
   if(!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftAlt) || Input.GetMouseButton(1))return;
   if(OverUI()||!MouseOnGround(out Vector3 p))return;
   if(currentTool==Tool.Select){
    Ray pick=sceneCamera.ScreenPointToRay(Input.mousePosition);
    if(Physics.Raycast(pick,out RaycastHit hit,500f)){
     selectedIndex=-1;
     for(int j=0;j<objects.Count;j++){
      if(objects[j]==hit.collider.gameObject||hit.collider.transform.IsChildOf(objects[j].transform)){selectedIndex=j;break;}
     }
    }else selectedIndex=-1;
    return;
   }
   if(currentTool==Tool.Erase) { Erase(p);return; }
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
   if(currentTool==Tool.Wall){
    Vector3 diff=p-start;
    Add(new Piece("wall",(start+p)*.5f+Vector3.up*wallHeight*.5f,new Vector3(diff.magnitude,wallHeight,wallThickness),Quaternion.Euler(0,-Mathf.Atan2(diff.z,diff.x)*Mathf.Rad2Deg,0)));
   }else{
    Vector3 delta=p-start;
    Add(new Piece("floor",(start+p)*.5f+Vector3.up*FloorCenter,new Vector3(Mathf.Max(Mathf.Abs(delta.x),gridSize),FloorThickness,Mathf.Max(Mathf.Abs(delta.z),gridSize)),Quaternion.identity));
   }
  }
  void Add(Piece piece){selectedIndex=-1;project.pieces.Add(piece);objects.Add(Create(piece));}
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
   selectedIndex=-1;
   for(int i=objects.Count-1;i>=0;i--){
    if(objects[i]==null)continue;
    var collider=objects[i].GetComponent<Collider>();
    Vector3 closest=collider.ClosestPoint(point+Vector3.up*.5f);
    if(Vector3.Distance(closest,point+Vector3.up*.5f)<1f){
     Destroy(objects[i]);objects.RemoveAt(i);project.pieces.RemoveAt(i);return;
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
    objects.Clear();selectedIndex=-1;project=loaded;
    foreach(var piece in project.pieces)objects.Add(Create(piece));Notice("Loaded: "+Path.GetFileName(filePath));
   }catch(Exception e){Debug.LogError("Load failed: "+e.Message);Notice("Load failed: "+e.Message);}
  }
  // A game-like, responsive editor shell. Current tools remain functional;
  // unimplemented features are explicitly marked as coming soon.
  private GUIStyle panelStyle, headingStyle, captionStyle, menuStyle, activeStyle, regularStyle;
  private Texture2D darkTexture, sideTexture, blueTexture, fieldTexture;
  Texture2D Swatch(Color color){
   var texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();return texture;
  }
  void InitStyles(){
   if(panelStyle!=null)return;
   darkTexture=Swatch(new Color(.065f,.095f,.125f,.96f));
   sideTexture=Swatch(new Color(.08f,.115f,.15f,.93f));
   blueTexture=Swatch(new Color(.105f,.35f,.66f,.96f));
   fieldTexture=Swatch(new Color(.14f,.19f,.25f,.96f));
   panelStyle=new GUIStyle(GUI.skin.box);
   panelStyle.normal.background=darkTexture;
   panelStyle.border=new RectOffset(0,0,0,0);
   panelStyle.normal.textColor=Color.white;
   panelStyle.alignment=TextAnchor.UpperCenter;
   panelStyle.fontSize=13;
   headingStyle=new GUIStyle(GUI.skin.label);
   headingStyle.normal.textColor=new Color(.94f,.97f,1f);
   headingStyle.fontSize=17;headingStyle.fontStyle=FontStyle.Bold;
   captionStyle=new GUIStyle(GUI.skin.label);
   captionStyle.normal.textColor=new Color(.68f,.76f,.83f);
   captionStyle.fontSize=12;
   menuStyle=new GUIStyle(GUI.skin.button);
   menuStyle.normal.background=sideTexture;
   menuStyle.hover.background=fieldTexture;
   menuStyle.active.background=blueTexture;
   menuStyle.normal.textColor=Color.white;
   menuStyle.hover.textColor=Color.white;
   menuStyle.fontSize=13;
   menuStyle.alignment=TextAnchor.MiddleCenter;
   activeStyle=new GUIStyle(menuStyle);
   activeStyle.normal.background=blueTexture;
   regularStyle=new GUIStyle(GUI.skin.textField);
   regularStyle.normal.background=fieldTexture;
   regularStyle.normal.textColor=Color.white;
   regularStyle.fontSize=13;
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
   GUI.Box(new Rect(0,0,w,66),"",panelStyle);
   if(Button(new Rect(12,9,45,46),"⌂")){showPause=true;}
   Label(new Rect(69,9,245,29),Path.GetFileNameWithoutExtension(filePath),true);
   Label(new Rect(69,34,255,22),"WORLDBUILDER  |  VRChat worlds");
   float start=Mathf.Max(320,w*.37f);
   float width=Mathf.Min(118,Mathf.Max(82,(w-750)/4f));
   string[] modes={"Build","Decorate","Landscape","Live"};
   for(int k=0;k<4;k++){
    int n=k;
    if(Button(new Rect(start+k*(width+7),12,width,40),modes[k],creativeMode==k)){
     if(k==3)EnterWalk();
     else {creativeMode=n;if(n>0)ComingSoon(modes[n]+" tools");}
    }
   }
   float x=w-296;
   if(Button(new Rect(x,12,61,40),"Undo"))UndoLast();
   if(Button(new Rect(x+67,12,64,40),"Save"))Save();
   if(Button(new Rect(x+137,12,64,40),"Assets"))ToggleAssetBrowser();
   if(Button(new Rect(x+207,12,73,40),"Menu"))showPause=!showPause;
  }
  void DrawLeft(float h){
   GUI.Box(new Rect(0,66,92,h-214),"",panelStyle);
   string[] names={"Select","Walls","Floors","Rooms","Doors","Windows","Stairs","Roofs","Furniture","Materials","Lighting","Terrain"};
   Tool[] tools={Tool.Select,Tool.Wall,Tool.Floor,Tool.Select,Tool.Select,Tool.Select,Tool.Select,Tool.Select,Tool.Imported,Tool.Select,Tool.Select,Tool.Select};
   float areaHeight=h-248;
   float step=Mathf.Clamp(areaHeight/12f,36f,56f);
   for(int k=0;k<names.Length;k++){
    bool active=(k==0&&currentTool==Tool.Select)||(k==1&&currentTool==Tool.Wall)||
      (k==2&&currentTool==Tool.Floor)||(k==8&&currentTool==Tool.Imported);
    if(Button(new Rect(7,77+k*step,78,step-4),names[k],active)){
     if(k==0||k==1||k==2)SelectTool(tools[k]);
     else if(k==8){creativeMode=1;catalogCategory=0;Notice("Choose an asset from My Assets below");}
     else ComingSoon(names[k]);
    }
   }
  }
  void DrawProperties(float w,float h){
   float x=w-282;
   GUI.Box(new Rect(x,77,270,h-259),"",panelStyle);
   Label(new Rect(x+17,92,238,27),selectedIndex>=0?"Selected Object":"Tool Properties",true);
   string name=selectedIndex>=0&&selectedIndex<project.pieces.Count?project.pieces[selectedIndex].kind:currentTool.ToString();
   Label(new Rect(x+17,129,240,24),name.ToUpperInvariant());
   float y=167;
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
    Label(new Rect(x+17,y,235,22),"POSITION (meters)");y+=27;
    float nextX=FloatField(new Rect(x+17,y,72,29),0,p.position.x);
    float nextY=FloatField(new Rect(x+100,y,72,29),1,p.position.y);
    float nextZ=FloatField(new Rect(x+183,y,72,29),2,p.position.z);
    y+=41;
    Label(new Rect(x+17,y,235,22),"SIZE (meters)");y+=25;
    float sx=FloatField(new Rect(x+17,y,72,29),3,p.scale.x);
    float sy=FloatField(new Rect(x+100,y,72,29),4,p.scale.y);
    float sz=FloatField(new Rect(x+183,y,72,29),5,p.scale.z);
    y+=42;
    if(Button(new Rect(x+17,y,235,32),"Apply Dimensions")){
     p.position=new Vector3(nextX,nextY,nextZ);
     p.scale=new Vector3(Mathf.Max(.02f,sx),Mathf.Max(.02f,sy),Mathf.Max(.02f,sz));
     if(objects[selectedIndex]!=null)Destroy(objects[selectedIndex]);
     objects[selectedIndex]=Create(p);
     editingPiece=-1;
     Notice("Object dimensions updated");
    }
    y+=49;
    if(Button(new Rect(x+17,y,235,32),"Delete Selected")){
     Destroy(objects[selectedIndex]);objects.RemoveAt(selectedIndex);
     project.pieces.RemoveAt(selectedIndex);selectedIndex=-1;
    }
   }else{
    Label(new Rect(x+17,y,235,36),"Select a placed object to edit it.");
    y+=45;
    Label(new Rect(x+17,y,230,26),"Grid snapping: "+gridSize.ToString("F2")+" m");
    y+=28;
    gridSize=GUI.HorizontalSlider(new Rect(x+17,y,232,20),gridSize,.25f,2f);y+=34;
    if(currentTool==Tool.Wall){
     Label(new Rect(x+17,y,220,25),"Wall height: "+wallHeight.ToString("F2")+" m");y+=25;
     wallHeight=GUI.HorizontalSlider(new Rect(x+17,y,232,20),wallHeight,1.5f,5f);y+=39;
     Label(new Rect(x+17,y,220,25),"Thickness: "+wallThickness.ToString("F2")+" m");y+=25;
     wallThickness=GUI.HorizontalSlider(new Rect(x+17,y,232,20),wallThickness,.08f,.5f);
    }
    if(currentTool==Tool.Imported){
     y+=28;
     Label(new Rect(x+17,y,220,25),"Object scale: "+importedScale.ToString("F2")+"x");y+=29;
     importedScale=GUI.HorizontalSlider(new Rect(x+17,y,232,18),importedScale,.1f,4f);y+=33;
     Label(new Rect(x+17,y,220,25),"Rotation: "+importedRotation.ToString("F0")+"°");y+=27;
     importedRotation=GUI.HorizontalSlider(new Rect(x+17,y,232,18),importedRotation,0f,360f);
    }
   }
   float bottom=h-290;
   Label(new Rect(x+17,bottom,235,25),"AVATAR SCALE PREVIEW");
   Label(new Rect(x+17,bottom+27,235,24),avatarHeight.ToString("F2")+" m tall");
   avatarHeight=GUI.HorizontalSlider(new Rect(x+17,bottom+55,230,17),avatarHeight,1.2f,2.2f);
  }
  float FloatField(Rect r,int index,float value){
   dimensionDraft[index]=GUI.TextField(r,dimensionDraft[index]??value.ToString("F2"),regularStyle);
   if(float.TryParse(dimensionDraft[index],out float number))return number;
   return value;
  }
  void DrawCatalog(float w,float h){
   float top=h-174;
   GUI.Box(new Rect(95,top,w-95,174),"",panelStyle);
   string[] categories={"My Assets","Furniture","Lighting","Decor","Kitchen","Bathroom","Plants","Outdoor","Materials"};
   float buttonWidth=Mathf.Min(110,(w-455)/categories.Length);
   for(int k=0;k<categories.Length;k++){
    if(Button(new Rect(108+k*(buttonWidth+4),top+10,buttonWidth,31),categories[k],catalogCategory==k))catalogCategory=k;
   }
   catalogSearch=GUI.TextField(new Rect(Mathf.Max(110,w-365),top+10,235,31),catalogSearch,regularStyle);
   if(Button(new Rect(w-120,top+10,109,31),"Get Assets"))ToggleAssetBrowser();
   if(Button(new Rect(Mathf.Max(110,w-480),top+10,108,31),"Import OBJ")){showImport=!showImport;showExport=false;}
   if(catalogCategory==0){
    var entries=AssetHub.Catalog();
    var visible=new List<string>();
    foreach(string id in entries)if(catalogSearch.Length==0||AssetHub.Name(id).IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0)visible.Add(id);
    float width=w-115;
    catalogScroll=GUI.BeginScrollView(new Rect(108,top+52,width,112),catalogScroll,
      new Rect(0,0,Mathf.Max(width,visible.Count*135),90));
    for(int k=0;k<visible.Count;k++){
     string id=visible[k];
     if(Button(new Rect(k*135,6,128,76),AssetHub.Name(id))){
      importedId=id;SelectTool(Tool.Imported);
      Notice("Click the world to place "+AssetHub.Name(id));
     }
    }
    GUI.EndScrollView();
    if(visible.Count==0)Label(new Rect(125,top+90,500,30),"No imported models yet. Click Get Assets to browse.");
   }else if(catalogCategory==1){
    if(Button(new Rect(110,top+58,138,73),"Sofa - Basic"))SelectTool(Tool.Couch);
    if(Button(new Rect(255,top+58,138,73),"Table - Basic"))SelectTool(Tool.Table);
    Label(new Rect(420,top+82,460,24),"More furniture will appear here as you import assets.");
   }else Label(new Rect(118,top+80,550,28),"Coming soon - this section is reserved for future assets.");
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
   Label(new Rect(x+25,y+167,455,27),"Ctrl+S Save   Ctrl+O Reload   Esc Menu");
   Label(new Rect(x+25,y+206,455,26),"CAMERA");
   Label(new Rect(x+25,y+239,455,28),"Right mouse: orbit    Scroll: zoom");
   Label(new Rect(x+25,y+271,455,28),"Walk Preview: WASD, mouse look, Esc to return");
   Label(new Rect(x+25,y+322,455,27),"WorldBuilder is currently in development.");
   if(Button(new Rect(x+24,y+397,452,42),"Back to Building"))showSettings=false;
  }
  void OnGUI(){
   InitStyles();
   float w=Screen.width,h=Screen.height;
   if(walkthrough!=null&&walkthrough.IsWalking){
    GUI.Box(new Rect(0,0,w,48),"",panelStyle);
    Label(new Rect(19,10,Mathf.Min(w-35,840),30),"LIVE PREVIEW     WASD Move   |   Mouse Look   |   Esc Return");
    return;
   }
   DrawToolbar(w);
   DrawLeft(h);
   DrawProperties(w,h);
   DrawCatalog(w,h);
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
   if(Time.time<statusUntil&&!showPause){
    GUI.Box(new Rect(97,h-215,Mathf.Min(w-400,690),32),"",panelStyle);
    Label(new Rect(110,h-211,Mathf.Min(w-425,660),25),status);
   }
  }
 }
}
