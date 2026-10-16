Shader "WorldBuilder/Lit" {
 Properties {
  _Color ("Color", Color) = (1,1,1,1)
  _MainTex ("Albedo", 2D) = "white" {}
 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 200
  CGPROGRAM
  #pragma surface surf Lambert
  #pragma target 3.0
  sampler2D _MainTex;
  fixed4 _Color;
  struct Input { float2 uv_MainTex; };
  void surf(Input IN, inout SurfaceOutput o) {
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   o.Albedo=c.rgb;
   o.Alpha=c.a;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
