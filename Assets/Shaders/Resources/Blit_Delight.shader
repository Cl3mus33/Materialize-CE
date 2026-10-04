// Materialize CE: shading removal from a photo, guided by the normal map and the AO map.
// The photo is modelled as albedo x shading, with shading = AO x (ambient + sun on the tilted surface): dividing
// the photo by that shading gives back a flatter albedo, where the luminance-only tools cannot tell a dark
// material from a shadowed one.
Shader "Hidden/Blit_Delight" {
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
			sampler2D _NormalTex;
			sampler2D _AOTex;
			float4 _LightDir;       // unit vector towards the light, in the texture's space (x right, y up, z out)
			float _ShadeStrength;   // 0..1: how much of the directional shading is removed
			float _AOStrength;      // 0..1: how much of the occlusion is removed
			float _FlipNormalY;     // 1: OpenGL normals (green up), 0: DirectX
			float _HasNormal, _HasAO;

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
				float3 photo = tex2Dlod( _MainTex, float4( IN.uv, 0, 0 ) ).rgb;

				float shade = 1.0;
				if ( _HasNormal > 0.5 && _ShadeStrength > 0.001 ) {
					float3 n = tex2Dlod( _NormalTex, float4( IN.uv, 0, 0 ) ).xyz * 2.0 - 1.0;
					if ( _FlipNormalY < 0.5 ) n.y = -n.y;
					n = normalize( n );
					const float ambient = 0.35;
					float lit = ambient + ( 1.0 - ambient ) * saturate( dot( n, _LightDir.xyz ) );
					float flat = ambient + ( 1.0 - ambient ) * saturate( _LightDir.z );
					shade = lerp( 1.0, lit / max( flat, 0.05 ), _ShadeStrength );
				}
				if ( _HasAO > 0.5 && _AOStrength > 0.001 ) {
					float ao = tex2Dlod( _AOTex, float4( IN.uv, 0, 0 ) ).x;
					shade *= lerp( 1.0, max( ao, 0.2 ), _AOStrength );
				}
				// The photo is gamma-encoded: the division is done in linear light. Never brightened more than about
				// 3.5 times: deep shadows hold noise, not colour.
				float3 albedo = photo / pow( max( shade, 0.0625 ), 1.0 / 2.2 );
				return float4( saturate( albedo ), 1.0 );
			}
			ENDCG
		}
	}
}
