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
  public enum Tool { Wall, Floor, Couch, Table, Erase }
  public Tool currentTool=Tool.Wall;
  private Vector3? anchor;
  private Project project=new Project();
  private readonly List<GameObject> objects=new List<GameObject>();
  private string filePath;
  private bool showHelp=true;
  private string status="Ready to build";
  private float statusUntil;
  private bool showLibrary=true;
  private bool showProjects=false;
  private GameObject preview;
  private bool showGrid=true;
  private readonly List<GameObject> gridMarkers=new List<GameObject>();
  bool OverUI(){
   if(walkthrough!=null&&walkthrough.IsWalking)return true;
   Vector2 p=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
   return p.y<66f||p.y>Screen.height-54f||
     (showLibrary&&p.x<205f)||
     (showProjects&&p.x>Screen.width-365f&&p.y<520f)||
     (showHelp&&p.x>Screen.width-380f&&p.y>Screen.height-215f);
  }
  void UndoLast(){
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
   if(currentTool==Tool.Couch||currentTool==Tool.Table){
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
  void NewProject(){project=new Project();foreach(var obj in objects)if(obj!=null)Destroy(obj);objects.Clear();anchor=null;Notice("New empty project. Save to keep it.");}
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
   if(Input.GetKeyDown(KeyCode.Alpha1))SelectTool(Tool.Wall);
   if(Input.GetKeyDown(KeyCode.Alpha2))SelectTool(Tool.Floor);
   if(Input.GetKeyDown(KeyCode.Alpha3))SelectTool(Tool.Couch);
   if(Input.GetKeyDown(KeyCode.Alpha4))SelectTool(Tool.Table);
   if(Input.GetKeyDown(KeyCode.Alpha5))SelectTool(Tool.Erase);
   if(Input.GetKeyDown(KeyCode.Escape))anchor=null;
   if(Input.GetKeyDown(KeyCode.F1))showHelp=!showHelp;
   if(Input.GetKeyDown(KeyCode.G))ToggleGrid();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))UndoLast();
   UpdatePreview();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.S))Save();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.O))Load();
   if(!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftAlt) || Input.GetMouseButton(1))return;
   if(OverUI()||!MouseOnGround(out Vector3 p))return;
   if(currentTool==Tool.Erase) { Erase(p);return; }
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
  void Add(Piece piece){project.pieces.Add(piece);objects.Add(Create(piece));}
  GameObject Create(Piece piece){
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
   for(int i=objects.Count-1;i>=0;i--){
    if(objects[i]==null)continue;
    var collider=objects[i].GetComponent<Collider>();
    Vector3 closest=collider.ClosestPoint(point+Vector3.up*.5f);
    if(Vector3.Distance(closest,point+Vector3.up*.5f)<1f){
     Destroy(objects[i]);objects.RemoveAt(i);project.pieces.RemoveAt(i);return;
    }
   }
  }
  public void Save(){
   try{File.WriteAllText(filePath,JsonUtility.ToJson(project,true));Debug.Log("Saved "+filePath);Notice("Saved: "+Path.GetFileName(filePath));}
   catch(Exception e){Debug.LogError(e);Notice("Save failed: "+e.Message);}
  }
  public void Load(){
   try{
    var loaded=JsonUtility.FromJson<Project>(File.ReadAllText(filePath));
    if(loaded==null||loaded.schema!=2||loaded.pieces==null)throw new InvalidDataException("Unsupported project schema");
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();project=loaded;
    foreach(var piece in project.pieces)objects.Add(Create(piece));Notice("Loaded: "+Path.GetFileName(filePath));
   }catch(Exception e){Debug.LogError("Load failed: "+e.Message);Notice("Load failed: "+e.Message);}
  }
  void OnGUI(){
   float w=Screen.width,h=Screen.height;
   GUI.skin.box.fontSize=14;
   GUI.Box(new Rect(0,0,w,66),"");
   GUI.Label(new Rect(18,9,230,30),"WORLDBUILDER 3D");
   GUI.Label(new Rect(18,35,270,20),"BUILD STUDIO  |  PREVIEW");
   if(walkthrough!=null&&walkthrough.IsWalking){
    GUI.Box(new Rect(0,0,w,66),"WALK PREVIEW  |  WASD move  |  Mouse look  |  Esc return to Build");
    GUI.Box(new Rect(0,h-54,w,54),"Avatar height: "+avatarHeight.ToString("F2")+" m  |  Spatial scale preview, not a full VRChat simulator");
    return;
   }
   if(GUI.Button(new Rect(w-555,15,120,36),"Walk Preview")){
    if(walkthrough!=null){if(preview!=null)preview.SetActive(false);walkthrough.SetHeight(avatarHeight);walkthrough.Enter();}
    else Notice("Walkthrough camera unavailable");
   }
   if(GUI.Button(new Rect(w-425,15,75,36),"New"))NewProject();
   if(GUI.Button(new Rect(w-342,15,75,36),"Save"))Save();
   if(GUI.Button(new Rect(w-259,15,75,36),"Save As"))SaveAsNamed("World-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
   if(GUI.Button(new Rect(w-176,15,75,36),"Projects"))showProjects=!showProjects;
   if(GUI.Button(new Rect(w-93,15,75,36),"Help"))showHelp=!showHelp;
   if(showLibrary){
    GUI.Box(new Rect(0,66,205,h-120),"BUILD LIBRARY");
    GUI.Label(new Rect(15,105,170,24),"STRUCTURE");
    if(GUI.Button(new Rect(14,134,176,42),"1   Walls"))SelectTool(Tool.Wall);
    if(GUI.Button(new Rect(14,182,176,42),"2   Floors"))SelectTool(Tool.Floor);
    GUI.Label(new Rect(15,247,170,24),"FURNITURE");
    if(GUI.Button(new Rect(14,276,176,42),"3   Sofa"))SelectTool(Tool.Couch);
    if(GUI.Button(new Rect(14,324,176,42),"4   Table"))SelectTool(Tool.Table);
    GUI.Label(new Rect(15,387,170,24),"EDIT");
    if(GUI.Button(new Rect(14,415,176,42),"5   Eraser"))SelectTool(Tool.Erase);
    if(GUI.Button(new Rect(14,474,176,38),showGrid?"Hide grid (G)":"Show grid (G)"))ToggleGrid();
    if(GUI.Button(new Rect(14,520,176,38),"Undo (Ctrl+Z)"))UndoLast();
    GUI.Label(new Rect(14,h-240,180,28),"Selected: "+currentTool);
    GUI.Label(new Rect(14,h-158,180,24),"AVATAR HEIGHT");
    GUI.Label(new Rect(14,h-132,180,22),avatarHeight.ToString("F2")+" m");
    avatarHeight=GUI.HorizontalSlider(new Rect(14,h-108,176,20),avatarHeight,1.2f,2.2f);
   }
   if(showProjects){
    float px=Mathf.Max(215,w-365);
    GUI.Box(new Rect(px,72,350,Mathf.Min(h-145,450)),"PROJECTS");
    if(GUI.Button(new Rect(px+12,110,326,34),"Open latest saved project"))Load();
    string[] paths=Directory.GetFiles(ProjectsFolder(),"*.json");
    Array.Sort(paths);Array.Reverse(paths);
    for(int i=0;i<Mathf.Min(paths.Length,6);i++){
     string p=paths[i];
     if(GUI.Button(new Rect(px+12,152+i*42,326,36),Path.GetFileNameWithoutExtension(p)))OpenProject(p);
    }
   }
   GUI.Box(new Rect(0,h-54,w,54),"");
   GUI.Label(new Rect(18,h-40,Mathf.Max(200,w-45),25),
    (Time.time<statusUntil?status:"Ready")+
    "     |     Click two points for walls/floors     |     Right drag: orbit     |     Wheel: zoom");
   if(showHelp){
    GUI.Box(new Rect(Mathf.Max(215,w-380),h-215,365,148),"QUICK START");
    GUI.Label(new Rect(Mathf.Max(230,w-365),h-185,340,25),"1-5 select tools  |  Esc cancels");
    GUI.Label(new Rect(Mathf.Max(230,w-365),h-158,340,25),"Left click to place  |  Right drag to orbit");
    GUI.Label(new Rect(Mathf.Max(230,w-365),h-131,340,25),"Ctrl+S save  |  Ctrl+O reload");
    GUI.Label(new Rect(Mathf.Max(230,w-365),h-104,340,25),"Ctrl+Z undo placement | G toggle grid");
   }
  }
 }
}
