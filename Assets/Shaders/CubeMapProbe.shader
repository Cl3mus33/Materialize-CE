Shader "Custom/CubeMapProbe" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_CubeMap ("Cube Map", CUBE) = "" {}
		_Blur( "Blur" , Float ) = 2.0
		_Factor( "Factor" , Float ) = 1.0
	}
	SubShader {
		Tags { "RenderQueue"="Opaque" "RenderType"="Opaque" }
		LOD 200
		Cull Front

		Pass {

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			samplerCUBE _CubeMap;
			float _Blur;
			float _Factor;
			
			uniform samplerCUBE _GlobalCubemap;
			// Materialize CE: your own HDRI (equirectangular), its rotation and brightness.
			uniform sampler2D _GlobalEquirect;
			uniform float _EnvEquirect;
			uniform float _EnvRotation;
			uniform float _EnvExposure;
			uniform samplerCUBE _ProbeCubemap;

			// vertex-to-fragment interpolation data
			struct v2f {
				float4 pos : SV_POSITION;
				float3 localNormal : TEXCOORD0;
			};

			// vertex shader
			v2f vert (appdata_full v) {
				v2f o;
				o.pos = UnityObjectToClipPos ( v.vertex );
				o.localNormal = v.normal.xyz;
				return o;
			}
			
			// fragment shader
			fixed4 frag (v2f IN) : SV_Target {
				
				fixed3 localNormal = normalize( IN.localNormal.xyz );
				
				float s = sin( _EnvRotation ), c = cos( _EnvRotation );
				float3 dir = float3( c * localNormal.x - s * localNormal.z, localNormal.y, s * localNormal.x + c * localNormal.z );

				half3 ambIBL;
				if ( _EnvEquirect > 0.5 ) {
					float2 uv = float2( atan2( dir.x, dir.z ) / 6.2831853 + 0.5, asin( clamp( dir.y, -1.0, 1.0 ) ) / 3.1415927 + 0.5 );
					ambIBL = tex2Dlod( _GlobalEquirect, float4( uv, 0, 0 ) ).xyz;
				} else {
					ambIBL = texCUBElod(_GlobalCubemap, half4( dir , 0.0 ) ).xyz;
				}
				float exposure = _EnvExposure > 0.0 ? _EnvExposure : 1.0;
				// Real HDR data needs no boost (the built-in 8-bit cubemaps get one to fake their highlights).
				if ( _EnvEquirect > 0.5 ) return float4( ambIBL * exposure * _Factor, 1.0 );

				return float4( ( ambIBL + ( ambIBL * ambIBL ) ) * exposure * _Factor, 1.0 );
				
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
