Shader "WorldBuilder/GradientSky" {
 Properties {
  _SkyTop ("Zenith", Color) = (0.24, 0.51, 0.83, 1)
  _SkyHorizon ("Horizon", Color) = (0.72, 0.84, 0.95, 1)
  _SkyGround ("Below Horizon", Color) = (0.51, 0.61, 0.66, 1)
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
   fixed4 _SkyTop;
   fixed4 _SkyHorizon;
   fixed4 _SkyGround;
   struct appdata { float4 vertex : POSITION; };
   struct v2f { float4 vertex : SV_POSITION; float3 dir : TEXCOORD0; };
   v2f vert(appdata v){
    v2f o;
    o.vertex=UnityObjectToClipPos(v.vertex);
    o.dir=mul((float3x3)unity_ObjectToWorld,v.vertex.xyz);
    return o;
   }
   fixed4 frag(v2f i) : SV_Target {
    float elevation=normalize(i.dir).y;
    float t=saturate(elevation*1.15);
    float u=smoothstep(-0.48,0.0,elevation);
    fixed3 upper=lerp(_SkyHorizon.rgb,_SkyTop.rgb,t);
    fixed3 lower=lerp(_SkyGround.rgb,_SkyHorizon.rgb,u);
    return fixed4(elevation>=0?upper:lower,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
