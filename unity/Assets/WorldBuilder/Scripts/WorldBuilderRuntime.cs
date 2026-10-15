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
  void Start() {
   if(sceneCamera==null)sceneCamera=Camera.main;
   filePath=Path.Combine(Application.persistentDataPath,"WorldBuilder-project.json");
   if(File.Exists(filePath))Load();
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
   if(Input.GetKeyDown(KeyCode.Alpha1))currentTool=Tool.Wall;
   if(Input.GetKeyDown(KeyCode.Alpha2))currentTool=Tool.Floor;
   if(Input.GetKeyDown(KeyCode.Alpha3))currentTool=Tool.Couch;
   if(Input.GetKeyDown(KeyCode.Alpha4))currentTool=Tool.Table;
   if(Input.GetKeyDown(KeyCode.Alpha5))currentTool=Tool.Erase;
   if(Input.GetKeyDown(KeyCode.Escape))anchor=null;
   if(Input.GetKeyDown(KeyCode.F1))showHelp=!showHelp;
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.S))Save();
   if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.O))Load();
   if(!Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.LeftAlt) || Input.GetMouseButton(1))return;
   if(!MouseOnGround(out Vector3 p))return;
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
    Add(new Piece("floor",(start+p)*.5f-Vector3.up*.075f,new Vector3(Mathf.Max(Mathf.Abs(delta.x),gridSize),.15f,Mathf.Max(Mathf.Abs(delta.z),gridSize)),Quaternion.identity));
   }
  }
  void Add(Piece piece){project.pieces.Add(piece);objects.Add(Create(piece));}
  GameObject Create(Piece piece){
   GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
   obj.name="WB_"+piece.kind;
   obj.transform.SetPositionAndRotation(piece.position,piece.rotation);
   obj.transform.localScale=piece.scale;
   Material material=piece.kind=="wall"?wallMaterial:piece.kind=="floor"?floorMaterial:furnitureMaterial;
   if(material!=null)obj.GetComponent<Renderer>().sharedMaterial=material;
   else obj.GetComponent<Renderer>().material.color=piece.kind=="wall"?new Color(.85f,.86f,.88f):piece.kind=="floor"?new Color(.63f,.54f,.44f):new Color(.25f,.6f,.7f);
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
   try{File.WriteAllText(filePath,JsonUtility.ToJson(project,true));Debug.Log("Saved "+filePath);}
   catch(Exception e){Debug.LogError(e);}
  }
  public void Load(){
   try{
    var loaded=JsonUtility.FromJson<Project>(File.ReadAllText(filePath));
    if(loaded==null||loaded.schema!=2||loaded.pieces==null)throw new InvalidDataException("Unsupported project schema");
    foreach(var obj in objects)if(obj!=null)Destroy(obj);
    objects.Clear();project=loaded;
    foreach(var piece in project.pieces)objects.Add(Create(piece));
   }catch(Exception e){Debug.LogError("Load failed: "+e.Message);}
  }
  void OnGUI(){
   if(!showHelp)return;
   GUI.Box(new Rect(12,12,365,145),"WorldBuilder 3D Prototype");
   GUI.Label(new Rect(24,40,345,25),"1 Wall | 2 Floor | 3 Couch | 4 Table | 5 Erase");
   GUI.Label(new Rect(24,62,345,25),"Click endpoints to build. Esc cancels.");
   GUI.Label(new Rect(24,84,345,25),"Right-drag orbit | WASD move | Wheel zoom");
   GUI.Label(new Rect(24,106,345,25),"Ctrl+S save | Ctrl+O load | F1 hide help");
   GUI.Label(new Rect(24,128,345,25),"Selected: "+currentTool);
  }
 }
}
