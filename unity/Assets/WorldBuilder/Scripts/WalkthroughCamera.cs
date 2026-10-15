using UnityEngine;
namespace WorldBuilder {
 // Spatial-scale preview only, not an emulation of the VRChat SDK controller.
 [RequireComponent(typeof(Camera))]
 public class WalkthroughCamera : MonoBehaviour {
  public float avatarHeight=1.70f;
  public float eyeHeightRatio=.94f;
  public float walkingSpeed=3f;
  public float lookSensitivity=2.3f;
  public bool IsWalking {get;private set;}
  private OrbitCamera orbit;
  private CharacterController body;
  private float yaw, pitch;
  private Vector3 velocity;
  private Vector3 savedPosition;
  private Quaternion savedRotation;
  private bool savedOrbitEnabled;
  void Awake(){
   orbit=GetComponent<OrbitCamera>();
   GameObject player=new GameObject("WorldBuilder Walkthrough Body");
   body=player.AddComponent<CharacterController>();
   body.enabled=false;
  }
  public void SetHeight(float height){
   avatarHeight=Mathf.Clamp(height, .5f, 3f);
   if(body!=null){
    body.height=avatarHeight;
    body.radius=Mathf.Min(.24f,avatarHeight*.2f);
    body.center=new Vector3(0,avatarHeight*.5f,0);
   }
  }
  public void Enter(){
   if(IsWalking)return;
   savedPosition=transform.position;
   savedRotation=transform.rotation;
   savedOrbitEnabled=orbit!=null&&orbit.enabled;
   if(orbit!=null)orbit.enabled=false;
   SetHeight(avatarHeight);
   Vector3 target=orbit!=null?orbit.focus:Vector3.zero;
   // Begin a short distance away from the construction focus, facing the scene.
   Vector3 spawn=target-transform.forward*2f;
   spawn.y=0;
   body.transform.position=spawn;
   body.enabled=true;
   yaw=transform.eulerAngles.y;
   pitch=0f;
   velocity=Vector3.zero;
   IsWalking=true;
   Cursor.lockState=CursorLockMode.Locked;
   Cursor.visible=false;
  }
  public void Exit(){
   if(!IsWalking)return;
   IsWalking=false;
   body.enabled=false;
   transform.SetPositionAndRotation(savedPosition,savedRotation);
   if(orbit!=null)orbit.enabled=savedOrbitEnabled;
   Cursor.lockState=CursorLockMode.None;
   Cursor.visible=true;
  }
  void OnDisable(){if(IsWalking)Exit();}
  void OnDestroy(){if(body!=null)Destroy(body.gameObject);}
  void Update(){
   if(!IsWalking)return;
   if(Input.GetKeyDown(KeyCode.Escape)){Exit();return;}
   yaw+=Input.GetAxis("Mouse X")*lookSensitivity;
   pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*lookSensitivity,-84f,84f);
   Quaternion facing=Quaternion.Euler(0,yaw,0);
   Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
   input=Vector3.ClampMagnitude(input,1f);
   float speed=walkingSpeed*(Input.GetKey(KeyCode.LeftShift)?1.8f:1f);
   body.Move(facing*input*speed*Time.deltaTime);
   if(body.isGrounded && velocity.y<0)velocity.y=-1f;
   velocity.y+=Physics.gravity.y*Time.deltaTime;
   body.Move(velocity*Time.deltaTime);
   transform.SetPositionAndRotation(body.transform.position+Vector3.up*(avatarHeight*eyeHeightRatio),Quaternion.Euler(pitch,yaw,0));
  }
 }
}
