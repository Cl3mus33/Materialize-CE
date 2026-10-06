// Materialize CE: adjustments of an existing map (levels, contrast, invert, strength), from its original.
Shader "Hidden/Blit_Adjust" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_OutBlack ("Output black", Float) = 0
		_OutWhite ("Output white", Float) = 1
	}
	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D _MainTex;
	float _Mode;          // 0 grey map, 1 normal map, 2 colour
	float _InBlack, _InWhite, _Gamma;
	float _OutBlack, _OutWhite;   // output levels: what black and white become
	float _Contrast, _Brightness, _Invert;
	float _HeightMode, _Strength;   // height: depth scaled below white (0 = flat white)
	float _Saturation, _FlipGreen;
	float _PreInvert;     // the map is shown inverted (roughness): adjust it in those terms
	float4 _MainTex_TexelSize;
	float _BlurSigma;      // smoothing, in pixels (gaussian sigma)
	float4 _BlurDir;       // (1,0) horizontal pass, (0,1) vertical pass


	struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
	v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }

	float3 Levels (float3 c)
	{
		c = saturate( ( c - _InBlack ) / max( _InWhite - _InBlack, 1e-4 ) );
		c = pow( c, 1.0 / max( _Gamma, 0.01 ) );
		c = lerp( _OutBlack.xxx, _OutWhite.xxx, c );
		return saturate( ( c - 0.5 ) * _Contrast + 0.5 + _Brightness );
	}

	float4 frag (v2f IN) : SV_Target
	{
		float4 tex = tex2Dlod( _MainTex, float4( IN.uv, 0, 0 ) );
		if ( _Mode > 1.5 ) {
			float3 c = Levels( tex.rgb );
			float lum = dot( c, float3( 0.2126, 0.7152, 0.0722 ) );
			c = saturate( lerp( lum.xxx, c, _Saturation ) );
			if ( _Invert > 0.5 ) c = 1.0 - c;
			return float4( c, tex.a );
		}
		if ( _Mode > 0.5 ) {
			float3 n = tex.rgb * 2.0 - 1.0;
			n.xy *= _Strength;
			if ( _FlipGreen > 0.5 ) n.y = -n.y;
			n = normalize( float3( n.xy, max( n.z, 1e-3 ) ) );
			return float4( n * 0.5 + 0.5, 1.0 );
		}
		float g = _PreInvert > 0.5 ? 1.0 - tex.r : tex.r;
		float v = Levels( g.xxx ).x;
		if ( _Invert > 0.5 ) v = 1.0 - v;
		if ( _PreInvert > 0.5 ) v = 1.0 - v;
		if ( _HeightMode > 0.5 ) v = saturate( 1.0 - ( 1.0 - v ) * _Strength );
		return float4( v, v, v, 1.0 );
	}
	// Separable gaussian: two passes (horizontal then vertical), a few taps each, precise to a tenth of a pixel.
	float4 fragBlur (v2f IN) : SV_Target
	{
		float sigma = max( _BlurSigma, 0.01 );
		int radius = (int)min( ceil( sigma * 3.0 ), 24.0 );
		float2 stepUV = _BlurDir.xy * _MainTex_TexelSize.xy;
		float4 sum = 0;
		float total = 0;
		[loop]
		for ( int i = -24; i <= 24; i++ ) {
			if ( abs( i ) > radius ) continue;
			float w = exp( -( i * i ) / ( 2.0 * sigma * sigma ) );
			sum += tex2Dlod( _MainTex, float4( IN.uv + stepUV * i, 0, 0 ) ) * w;
			total += w;
		}
		return sum / total;
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
		Pass {
			ZTest Always Cull Off ZWrite Off Blend Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment fragBlur
			ENDCG
		}
	}
	Fallback off
}
