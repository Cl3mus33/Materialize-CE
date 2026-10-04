// Materialize CE: one dab of the clone stamp. Inside the brush, the map is replaced by itself read at an offset
// (the source point); the texture repeats, so a dab across the border retouches the seam.
Shader "Hidden/Blit_Retouch" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}

	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off Blend Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _Dest;       // xy: centre of the dab, in texture coordinates
			float4 _Offset;     // xy: source minus destination
			float4 _Aspect;     // xy: scale of each axis so the brush stays round on a non-square map
			float _Radius;      // in texture coordinates (of the larger side)
			float _Hardness;    // 0 soft .. 1 hard edge
			float _Opacity;

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert (appdata_img v) {
				v2f o;
				o.pos = UnityObjectToClipPos( v.vertex );
				o.uv = v.texcoord;
				return o;
			}

			float4 frag (v2f IN) : SV_Target {
				float4 here = tex2Dlod( _MainTex, float4( IN.uv, 0, 0 ) );
				// Shortest way to the dab on a repeating texture.
				float2 d = abs( IN.uv - _Dest.xy );
				d = min( d, 1.0 - d ) * _Aspect.xy;
				float dist = length( d ) / max( _Radius, 1e-5 );
				float mask = ( 1.0 - smoothstep( min( _Hardness, 0.98 ), 1.0, dist ) ) * _Opacity;
				if ( mask <= 0.0 ) return here;
				float4 from = tex2Dlod( _MainTex, float4( frac( IN.uv + _Offset.xy ), 0, 0 ) );
				return lerp( here, from, mask );
			}
			ENDCG
		}
	}
}
