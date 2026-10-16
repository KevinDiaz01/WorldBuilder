Shader "WorldBuilder/PanoramaSky" {
 Properties {
  _MainTex ("Equirectangular Panorama", 2D) = "grey" {}
  _Exposure ("Exposure", Range(0,5)) = 1
 }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off
  ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float _Exposure;
   struct vertex_input { float4 vertex:POSITION; };
   struct fragment_input { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; };
   fragment_input vert(vertex_input input){
    fragment_input o;
    o.pos=UnityObjectToClipPos(input.vertex);
    o.dir=mul((float3x3)unity_ObjectToWorld,input.vertex.xyz);
    return o;
   }
   half4 frag(fragment_input input):SV_Target {
    float3 dir=normalize(input.dir);
    const float pi=3.14159265358979323846;
    float2 uv=float2(atan2(dir.x,dir.z)/(2*pi)+0.5, asin(clamp(dir.y,-1,1))/pi+0.5);
    return half4(tex2D(_MainTex,uv).rgb*_Exposure,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
