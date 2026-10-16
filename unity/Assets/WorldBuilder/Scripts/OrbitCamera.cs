using UnityEngine;
namespace WorldBuilder {
 public class OrbitCamera : MonoBehaviour {
  public Vector3 focus=Vector3.zero;
  public float distance=18f;
  public float yaw=40f;
  public float pitch=48f;
  public float orbitSpeed=3f;
  public float moveSpeed=8f;
  private WorldBuilderRuntime editor;
  void Start(){
   editor=FindFirstObjectByType<WorldBuilderRuntime>();
   UpdateTransform();
  }
  void Update(){
   if(editor==null)editor=FindFirstObjectByType<WorldBuilderRuntime>();
   bool overControls=editor!=null&&editor.BlocksCameraPointer();
   if(Input.GetMouseButton(1)&&!overControls){
    yaw+=Input.GetAxis("Mouse X")*orbitSpeed;
    pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*orbitSpeed,12f,85f);
   }
   float scroll=overControls?0f:Input.mouseScrollDelta.y;
   distance=Mathf.Clamp(distance-scroll*2f,3f,100f);
   Vector3 forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
   Vector3 right=Quaternion.Euler(0,yaw,0)*Vector3.right;
   Vector3 delta=right*Input.GetAxisRaw("Horizontal")+forward*Input.GetAxisRaw("Vertical");
   focus+=delta.normalized*moveSpeed*Time.deltaTime*(Input.GetKey(KeyCode.LeftShift)?3f:1f);
   UpdateTransform();
  }
  void UpdateTransform(){
   Quaternion q=Quaternion.Euler(pitch,yaw,0);
   transform.position=focus-q*Vector3.forward*distance;
   transform.rotation=q;
  }
 }
}
