Shader "Custom/CubeMapBackground" {
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
			uniform samplerCUBE _ProbeCubemap;
			// Materialize CE: background as the environment (blur, brightness) or a plain colour.
			uniform float _BgSolid;
			uniform float4 _BgColor;
			uniform float _BgBlur;
			uniform float _BgBrightness;
			// Your own / bundled HDRI, read directly at its full resolution (sharper than through the probe).
			uniform sampler2D _GlobalEquirect;
			uniform float _EnvEquirect;
			uniform float _EnvRotation;
			uniform float _EnvExposure;

			// vertex-to-fragment interpolation data
			struct v2f {
				float4 pos : SV_POSITION;
				float3 worldNormal : TEXCOORD0;
			};

			// vertex shader
			v2f vert (appdata_full v) {
				v2f o;
				o.pos = UnityObjectToClipPos ( v.vertex );
				o.worldNormal = UnityObjectToWorldDir(v.normal.xyz);
				return o;
			}

			static const int BlurKernelSamples = 27;		
			static const float3 BlurKernel[BlurKernelSamples] =
			{
				float3(1,1,1),
				float3(0,1,1),
				float3(-1,1,1),

				float3(1,0,1),
				float3(0,0,1),
				float3(-1,0,1),

				float3(1,-1,1),
				float3(0,-1,1),
				float3(-1,-1,1),

				//====================//

				float3(1,1,0),
				float3(0,1,0),
				float3(-1,1,0),

				float3(1,0,0),
				float3(0,0,0),
				float3(-1,0,0),

				float3(1,-1,0),
				float3(0,-1,0),
				float3(-1,-1,0),

				//====================//

				float3(1,1,-1),
				float3(0,1,-1),
				float3(-1,1,-1),

				float3(1,0,-1),
				float3(0,0,-1),
				float3(-1,0,-1),

				float3(1,-1,-1),
				float3(0,-1,-1),
				float3(-1,-1,-1)
			};
			
			// fragment shader
			fixed4 frag (v2f IN) : SV_Target {
				
				fixed3 worldNormal = normalize( IN.worldNormal.xyz );

				if ( _BgSolid > 0.5 ) return float4( _BgColor.rgb, 1.0 );

				if ( _EnvEquirect > 0.5 ) {
					float s = sin( _EnvRotation ), c = cos( _EnvRotation );
					float3 n = worldNormal;
					float3 dir = float3( c * n.x - s * n.z, n.y, s * n.x + c * n.z );
					float2 uv = float2( atan2( dir.x, dir.z ) / 6.2831853 + 0.5, asin( clamp( dir.y, -1.0, 1.0 ) ) / 3.1415927 + 0.5 );
					// Blur through the mipmaps: 0 = the full 4K, 1 = very soft.
					float blurE = _BgBrightness > 0.0 ? _BgBlur : 0.25;
					float3 e = tex2Dlod( _GlobalEquirect, float4( uv, 0, blurE * 9.0 ) ).rgb * ( _EnvExposure > 0.0 ? _EnvExposure : 1.0 );
					if ( _BgBrightness > 0.0 ) e *= _BgBrightness / 0.7;
					return float4( e * _Factor, 1.0 );
				}

				// _BgBlur: 0 sharp .. 1 very soft (the original look is about 0.25). Unset (0) keeps the original.
				float blur = _BgBrightness > 0.0 ? _BgBlur : 0.25;
				float lod = blur * 4.0;
				float spread = 0.1 * blur;
				float3 ambIBL = 0.0;
				for( int i = 0; i < BlurKernelSamples; i++ ){
					ambIBL += texCUBElod(_ProbeCubemap, half4( worldNormal + BlurKernel[i] * spread , lod ) ).xyz;
				}
				ambIBL *= 1.0 / BlurKernelSamples;
				if ( _BgBrightness > 0.0 ) ambIBL *= _BgBrightness / 0.7;

				//ambIBL = texCUBElod(_ProbeCubemap, half4( worldNormal, 1.0 ) ).xyz;
				
				//return float4( ( ambIBL + ( ambIBL * ambIBL ) ) * _Factor, 1.0 );
				return float4( ambIBL * _Factor,1.0 );
				
			}
			ENDCG
		}
		
		/*
		Pass
		{
			Name "DEFERRED"
			Tags { "LightMode" = "Deferred" }
			Fog {Mode Off}
			
			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma target 3.0
			
			#pragma exclude_renderers nomrt
			#pragma multi_compile_prepassfinal
			#pragma multi_compile _USE_BAKED_CUBEMAP_ON _USE_BAKED_CUBEMAP_OFF
			#define UNITY_PASS_DEFERRED
			
			#include "HLSLSupport.cginc"
			#include "UnityShaderVariables.cginc"
			
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
		
			sampler2D _MainTex;
			samplerCUBE _CubeMap;
			float _Blur;
			float _Factor;

			float _UseProbeTexture;
			
			uniform samplerCUBE _GlobalCubemap;
			uniform samplerCUBE _ProbeCubemap;
			
			#include "DNMST.cginc"

			static const int BlurKernelSamples = 27;		
			static const float3 BlurKernel[BlurKernelSamples] =
			{
				float3(1,1,1),
				float3(0,1,1),
				float3(-1,1,1),

				float3(1,0,1),
				float3(0,0,1),
				float3(-1,0,1),

				float3(1,-1,1),
				float3(0,-1,1),
				float3(-1,-1,1),

				//====================//

				float3(1,1,0),
				float3(0,1,0),
				float3(-1,1,0),

				float3(1,0,0),
				float3(0,0,0),
				float3(-1,0,0),

				float3(1,-1,0),
				float3(0,-1,0),
				float3(-1,-1,0),

				//====================//

				float3(1,1,-1),
				float3(0,1,-1),
				float3(-1,1,-1),

				float3(1,0,-1),
				float3(0,0,-1),
				float3(-1,0,-1),

				float3(1,-1,-1),
				float3(0,-1,-1),
				float3(-1,-1,-1)
			};
			
			void frag_surf (v2f_surf IN, out half4 outDiffuse : SV_Target0, out half4 outMST : SV_Target1, out half4 outNormal : SV_Target2, out half4 outEmission : SV_Target3) {
				// Albedo comes from a texture tinted by color
				
				fixed3 localNormal = float3(0,0,1);

				fixed3 worldNormalBaked = normalize( IN.localNormal.xyz );
				fixed3 worldNormal = normalize( float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z ) );
				
				float3 worldPos = float3(IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w);
				fixed3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );


				float3 ambIBL = 0.0;
				for( int i = 0; i < BlurKernelSamples; i++ ){
					ambIBL += texCUBElod(_ProbeCubemap, half4( worldNormal + BlurKernel[i] * 0.025 , 1.0 ) ).xyz;
				}
				ambIBL *= 1.0 / BlurKernelSamples;

				#if _USE_BAKED_CUBEMAP_ON
					float3 ambIBLbaked = 0.0;
					for( int i = 0; i < BlurKernelSamples; i++ ){
						ambIBLbaked += texCUBElod(_GlobalCubemap, half4( worldNormalBaked + BlurKernel[i] * 0.025 , 3.0 ) ).xyz;
					}
					ambIBLbaked *= 1.0 / BlurKernelSamples;

					ambIBL = lerp( ambIBLbaked, ambIBL, _UseProbeTexture );
				#endif

				
				SurfaceOutputStandard surfOut;
				surfOut.Albedo = float3(0,0,0);
				surfOut.Normal = worldNormal;
				surfOut.Metallic = 0;
				surfOut.Smoothness = 0;
				surfOut.Transmission = 0;
				surfOut.Emission =  ambIBL * _Factor;
				surfOut.Motion = float2(1,1);//float2(0.5,0.5);
				surfOut.Alpha = 1.0;
				surfOut.Occlusion = 0;
				
				ReturnOutput ( surfOut, worldPos, worldViewDir, IN, outDiffuse, outMST, outNormal, outEmission );
				
			}
			ENDCG
		} 
		*/
	}
	FallBack "Diffuse"
}
