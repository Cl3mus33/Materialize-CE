// Materialize CE: builds one output texture from up to four maps, one per channel.
// Each output channel reads any channel of any map (dot with _Pick), can be inverted, and multiplied by AO.
Shader "Hidden/Blit_Channel_Pack" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	CGINCLUDE

	#include "UnityCG.cginc"

	sampler2D _MainTex;
	sampler2D _Tex0, _Tex1, _Tex2, _Tex3;       // source map of R, G, B, A
	float4 _Pick0, _Pick1, _Pick2, _Pick3;      // which of its channels: (1,0,0,0) = red, luminance weights, ...
	float4 _Invert;                             // 1 = 1 - value, per output channel
	float4 _MulAO;                              // strength of the AO multiply, per output channel
	sampler2D _AOTex;

	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f vert(appdata_img v)
	{
		v2f o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
		return o;
	}

	float Channel(sampler2D tex, float4 pick, float invert, float mulAO, float ao, float2 uv)
	{
		float v = dot(tex2Dlod(tex, float4(uv, 0, 0)), pick);
		v = lerp(v, 1.0 - v, invert);
		return v * lerp(1.0, ao, mulAO);
	}

	float4 frag (v2f IN) : SV_Target
	{
		float2 uv = IN.uv;
		float ao = tex2Dlod(_AOTex, float4(uv, 0, 0)).x;
		return saturate(float4(
			Channel(_Tex0, _Pick0, _Invert.x, _MulAO.x, ao, uv),
			Channel(_Tex1, _Pick1, _Invert.y, _MulAO.y, ao, uv),
			Channel(_Tex2, _Pick2, _Invert.z, _MulAO.z, ao, uv),
			Channel(_Tex3, _Pick3, _Invert.w, _MulAO.w, ao, uv)));
	}

	ENDCG

	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off Blend Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}

	Fallback off
}
