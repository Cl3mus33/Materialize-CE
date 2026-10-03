// Materialize CE: maps of the Specular / Glossiness and Metallic / Roughness workflows, derived from the
// metallic / smoothness maps Materialize works with.
//   pass 0: specular colour = lerp(dielectric F0, albedo, metallic)
//   pass 1: diffuse of the specular workflow = albedo * (1 - metallic)
//   pass 2: inverted grey (roughness from smoothness)
Shader "Hidden/Blit_Workflow" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D _MainTex;      // albedo (passes 0, 1) or grey map (pass 2)
	sampler2D _MetalTex;

	struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
	v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }

	// 4 % reflectance of common dielectrics, in the sRGB space the maps are stored in.
	static const float3 DielectricF0 = float3(0.2231, 0.2231, 0.2231);

	float4 fragSpecular (v2f IN) : SV_Target
	{
		float3 albedo = tex2Dlod(_MainTex, float4(IN.uv, 0, 0)).rgb;
		float metal = tex2Dlod(_MetalTex, float4(IN.uv, 0, 0)).r;
		return float4(lerp(DielectricF0, albedo, metal), 1);
	}

	float4 fragDiffuse (v2f IN) : SV_Target
	{
		float4 albedo = tex2Dlod(_MainTex, float4(IN.uv, 0, 0));
		float metal = tex2Dlod(_MetalTex, float4(IN.uv, 0, 0)).r;
		return float4(albedo.rgb * (1.0 - metal), albedo.a);
	}

	float4 fragInvert (v2f IN) : SV_Target
	{
		float v = 1.0 - tex2Dlod(_MainTex, float4(IN.uv, 0, 0)).r;
		return float4(v, v, v, 1);
	}
	ENDCG

	SubShader {
		ZTest Always Cull Off ZWrite Off Blend Off
		Pass { CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragSpecular
			ENDCG }
		Pass { CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDiffuse
			ENDCG }
		Pass { CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragInvert
			ENDCG }
	}
	Fallback off
}
