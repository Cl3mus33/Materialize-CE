// Materialize CE: Skyrim-style preview. Same displacement (tessellation) and projection as the standard
// preview, with Skyrim's Blinn-Phong lighting instead of Unity's PBR:
//  _SkyrimMode 0 = vanilla: specular masked by the gloss map (the _n alpha), one glossiness for the whole mesh,
//                  AO baked into the diffuse like the exported texture.
//  _SkyrimMode 1 = Complex Material: glossiness per pixel, metal tints the specular, environment reflections
//                  masked by the metal (the _m red channel).
// Both: NIF specular colour, environment map masked by the metallic map (the _m environment mask of vanilla
// "Environment Map" shaders; import Skyrim's own cubemaps as HDRIs for the in-game look), and an optional
// filmic tone curve like NifSkope's (the SP Skyrim shader by Darkluke1111 was the reference for these).
// Vanilla follows NifSkope's sk_default shader (fo76utils fork, BSD licence): the specular is not scaled by N.L,
// the environment map is added to the colour before the lighting (so it darkens in shade), and the parallax
// is the game's texture offset (height * 0.08 - 0.04) instead of tessellation when "game-like parallax" is on.
Shader "Custom/Preview_Skyrim" {
	Properties {
		_DiffuseMap("Diffuse", 2D) = "grey" {}
		_NormalMap("Normal", 2D) = "bump" {}
		_SmoothnessMap("Smoothness", 2D) = "black" {}
		_MetallicMap("Metallic", 2D) = "black" {}
		_AOMap("Ambient Occlusion", 2D) = "white" {}
		_EdgeMap("Edge", 2D) = "grey" {}
		_DisplacementMap("Displacement", 2D) = "grey" {}
		
		_Tint("Diffuse Tint", Color) = (0.5,0.5,0.5,1)
		
		_AOPower ("AO Power", Float ) = 1.0
		_EdgePower ("Edge Power", Float ) = 1.0

		_Smoothness ("Smoothness", Float ) = 1.0
		_Metallic ("Metallic", Float ) = 1.0
		_Dust ("Dust", Float ) = 0.0
		
		_Parallax ("Height", Range (0.0, 3.0)) = 0.5
		_EdgeLength ("Edge length", Range(3,50)) = 3
		
		_Tiling ("Tiling", Vector ) = (1.0,1.0,0.0,0.0)

		//_TopProj ("Top Projection", Float ) = 0.0

		_DispOffset ("Displacement Offset", Float ) = 1.0

		_SkyrimMode ("Mode (0 vanilla, 1 complex material)", Float) = 0
		_SpecStrength ("Specular Strength (NIF)", Float) = 1
		_SpecPower ("Glossiness (NIF)", Float) = 80
		_EnvStrength ("Environment", Float) = 1
		_SpecColorNif ("Specular Color (NIF)", Color) = (1,1,1,1)
		_SkyrimTone ("Filmic tone curve", Float) = 1
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf SkyrimBP vertex:vert fullforwardshadows addshadow noforwardadd nometa nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass tessellate:tessEdge finalcolor:skyrimFinal

		// Use shader model 5.0 target, to get nicer looking lighting
		#pragma target 5.0

		#pragma multi_compile _ TOP_PROJECTION
		#include "UnityCG.cginc"
		#include "Tessellation.cginc"

		sampler2D _DiffuseMap;
		sampler2D _NormalMap;
		sampler2D _SmoothnessMap;
		sampler2D _MetallicMap;
		sampler2D _AOMap;
		sampler2D _EdgeMap;
		sampler2D _DisplacementMap;
		
		float4 _Tiling;
		
		float4 _Tint;
		float _AOPower;
		float _EdgePower;
		float _Smoothness;
		float _Metallic;
		float _Dust;
		
		float _GamaCorrection;
		
		int _FlipNormalY;

		//float _TopProj;

		float _Parallax;
		// Materialize CE: Adjust's reveal slider. Left of _AdjustReveal (along U) the adjusted map is shown flat,
		// right of it the lit material; a thin accent line marks the cut.
		float4 _DisplacementMap_TexelSize;
		uniform float _AdjustReveal;
		uniform float _AdjustRevealInvert;
		uniform sampler2D _AdjustRevealMap;

		float _SkyrimMode;
		float _SpecStrength;
		float _SpecPower;
		float _EnvStrength;
		float4 _SpecColorNif;
		float _SkyrimTone;
		float _SkyrimParallax;   // 1: the game's offset parallax, no tessellated displacement

		struct SurfaceOutputSkyrim {
			fixed3 Albedo;
			fixed3 Normal;
			half3 Emission;
			fixed Alpha;
			half SpecMask;
			half Gloss;
			half Metal;
			half Occlusion;
			half3 Ambient;   // the environment's light around the normal
		};

		half4 LightingSkyrimBP (SurfaceOutputSkyrim s, half3 viewDir, UnityGI gi)
		{
			half3 N = normalize( s.Normal );
			half3 L = gi.light.dir;
			half3 H = normalize( L + viewDir );
			half ndl = saturate( dot( N, L ) );
			half ndh = saturate( dot( N, H ) );
			bool complex = _SkyrimMode > 0.5;
			// Vanilla: one glossiness from the NIF. Complex Material: the gloss map scales it per pixel.
			half power = complex ? max( 2.0, s.Gloss * _SpecPower ) : max( 1.0, _SpecPower );
			half3 specColor = complex ? lerp( _SpecColorNif.rgb, s.Albedo, s.Metal ) : _SpecColorNif.rgb;
			half3 diffuseColor = complex ? s.Albedo * ( 1.0 - s.Metal * 0.75 ) : s.Albedo;
			// Clamped like the NIF shader: a highlight never goes past white.
			// Environment map, masked by the metallic map (the _m); Complex Material tints it by the metal.
			half3 envColor = complex ? lerp( half3(1,1,1), s.Albedo, s.Metal ) : half3(1,1,1);
			half3 env = gi.indirect.specular * s.Metal * _EnvStrength * envColor * s.Occlusion;
			half3 lighting = gi.light.color * ndl + gi.indirect.diffuse + s.Ambient;
			if ( !complex ) {
				// As the game (NifSkope's sk_default): specular not scaled by N.L, only by the light; the
				// environment added to the colour before the lighting.
				half3 spec = gi.light.color * saturate( pow( ndh, power ) * s.SpecMask * _SpecStrength * specColor );
				return half4( ( diffuseColor + env ) * lighting + spec, 1.0 );
			}
			half3 specCM = gi.light.color * saturate( pow( ndh, power ) * s.SpecMask * _SpecStrength * specColor ) * ndl;
			return half4( diffuseColor * lighting + specCM + env, 1.0 );
		}

		void LightingSkyrimBP_GI (SurfaceOutputSkyrim s, UnityGIInput data, inout UnityGI gi)
		{
			// Vanilla environment maps are sampled sharp; Complex Material blurs them by the gloss.
			half smooth = _SkyrimMode > 0.5 ? saturate( s.Gloss ) : 1.0;
			Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup( smooth, data.worldViewDir, s.Normal, half3(0.04, 0.04, 0.04) );
			gi = UnityGlobalIllumination( data, s.Occlusion, s.Normal, g );
		}
		float _DispOffset;
		float _EdgeLength;

		// Filmic curve (Hable), normalised so white stays white, as in NifSkope-style previews.
		half3 Hable( half3 x )
		{
			const half A = 0.15, B = 0.50, C = 0.10, D = 0.20, E = 0.02, F = 0.30;
			return ( ( x * ( A * x + C * B ) + D * E ) / ( x * ( A * x + B ) + D * F ) ) - E / F;
		}


		samplerCUBE _ProbeCubemap;

		float4 tessEdge (appdata_full v0, appdata_full v1, appdata_full v2 )
		{
			return UnityEdgeLengthBasedTess(v0.vertex, v1.vertex, v2.vertex, _EdgeLength );
		}

		// With tessellation the vertex function cannot pass data on: the top projection's frame is rebuilt
		// in surf from the world position and normal instead.
		struct Input {
			float2 uv_DiffuseMap;
			float3 worldPos;
			float3 worldNormal;
			float3 viewDir;
			INTERNAL_DATA
		};


		// Materialize CE: the tessellated vertices are about _EdgeLength pixels apart; far away, one vertex covers
		// many texels, so the height is read from the matching mip (otherwise the relief turns to noise from afar).
		uniform float4 _MceCamPos;      // xyz: camera, w: world size of one pixel at distance 1
		uniform float _MceUvPerWorld;   // texture repeats per world unit on the preview shape
		float DispLod( float3 objectPos, float4 texelSize, float tiling )
		{
			if ( _MceCamPos.w <= 0.0 ) return 0.0;
			float3 worldPos = mul( unity_ObjectToWorld, float4( objectPos, 1.0 ) ).xyz;
			float spacing = distance( worldPos, _MceCamPos.xyz ) * _MceCamPos.w * max( _EdgeLength, 1.0 );
			float texels = spacing * _MceUvPerWorld * tiling * texelSize.z;
			return max( 0.0, log2( max( texels, 1e-4 ) ) - 0.5 );
		}

		void vert (inout appdata_full v){
			float dispLod = DispLod( v.vertex.xyz, _DisplacementMap_TexelSize, _Tiling.x );

			float2 UV = v.texcoord.xy * _Tiling.xy + _Tiling.zw;
			float d = ( tex2Dlod(_DisplacementMap, float4(UV,0,dispLod)).x - _DispOffset ) * _Parallax * ( 1.0 / _Tiling.x );

			#ifdef TOP_PROJECTION
				float2 TPUV = v.vertex.xz * 0.14 * _Tiling.xy + _Tiling.zw + 0.5;
				float dy = ( tex2Dlod(_DisplacementMap, float4(TPUV,0,dispLod)).x - _DispOffset ) * _Parallax * ( 1.0 / _Tiling.x );

				float TexBlend = 1.0 - smoothstep( 0.25, 0.75, abs( v.normal.y * 0.8 ) );
				d = lerp( dy, d, TexBlend );
			#endif

			// The game's parallax is a texture offset (surf): no displacement then.
			if ( _SkyrimParallax < 0.5 && !( _AdjustReveal > 0.0 && v.texcoord.x < _AdjustReveal ) ) v.vertex.xyz += v.normal * d;

		}

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputSkyrim o) {

			float2 UV = IN.uv_DiffuseMap.xy * _Tiling.xy + _Tiling.zw;
			// Skyrim's parallax (Parallax shader type, and Complex Material's height): a plain texture offset
			// along the view, from the height map (NifSkope's sk_default).
			if ( _SkyrimParallax > 0.5 ) {
				float height = tex2D( _DisplacementMap, UV ).r;
				UV += normalize( IN.viewDir ).xy * ( height * 0.08 - 0.04 ) * saturate( _Parallax * 2.0 );
			}
			
			half3 texDiffuse = tex2D (_DiffuseMap, UV).xyz;
			half3 texNormal = tex2D(_NormalMap,UV).xyz;
			half3 texMetallic = tex2D(_MetallicMap,UV).xyz;
			half texSmoothness = tex2D(_SmoothnessMap,UV).x;
			half texAO = tex2D(_AOMap,UV).x;
			half texEdge = tex2D(_EdgeMap,UV).x;
			half texDisplace = tex2D(_DisplacementMap, UV).x;
			
			texNormal.xyz = texNormal.xyz * 2.0 - 1.0;
			if( _FlipNormalY == 0 ){
				texNormal.y *= -1.0;
			}

			fixed3 worldNormal = WorldNormalVector (IN, texNormal );

			// Get the tangent and binormal for the texcoord0 (this is just the actual tangent and binormal that comes in from the vertex shader)
			float3 worldVertTangent = WorldNormalVector (IN, float3(1,0,0) );
			float3 worldVertBinormal = WorldNormalVector (IN, float3(0,1,0) );
			float3 worldVertNormal = WorldNormalVector (IN, float3(0,0,1) );

			#ifdef TOP_PROJECTION

				float3 localPos = mul( unity_WorldToObject, float4( IN.worldPos, 1.0 ) ).xyz;
				float3 localNormalV = normalize( mul( (float3x3)unity_WorldToObject, worldVertNormal ) );
				float3 tangentLocal = abs( localNormalV.z ) < 0.999 ? normalize( cross( localNormalV, float3(0,0,-1) ) ) : float3(1,0,0);
				float3 tangentY = normalize( mul( (float3x3)unity_ObjectToWorld, tangentLocal ) );
				float3 binormalY = normalize( cross( worldVertNormal, tangentY ) );

				float2 TPUV = localPos.xz * 0.14 * _Tiling.xy + _Tiling.zw + 0.5;

				half3 texDiffuseY = tex2D (_DiffuseMap, TPUV).xyz;
				half3 texNormalY = tex2D(_NormalMap,TPUV).xyz;
				half3 texMetallicY = tex2D(_MetallicMap,TPUV).xyz;
				half texSmoothnessY = tex2D(_SmoothnessMap,TPUV).x;
				half texAOY = tex2D(_AOMap,TPUV).x;
				half texEdgeY = tex2D(_EdgeMap,TPUV).x;
				half texDisplaceY = tex2D(_DisplacementMap, TPUV).x;

				texNormalY.xyz = texNormalY.xyz * 2.0 - 1.0;
				if( _FlipNormalY == 0 ){
					texNormalY.y *= -1.0;
				}

				texNormalY.x *= step( localNormalV.y, 0 ) * 2 - 1;

				float3 worldNormalY = texNormalY.xyz;

				worldNormalY = ( worldNormalY.x * tangentY ) + ( worldNormalY.y * binormalY ) + (worldNormalY.z * worldVertNormal );

				half blendY = texDisplaceY + ( abs( localNormalV.y * 3.0 ) - 1.5 );

				half blendFalloff = 0.1;
				half SSHigh = 0.01 + ( saturate( blendFalloff ) );
				half SSLow = -0.01 - ( saturate( blendFalloff ) );

				half texBlend = smoothstep( SSLow, SSHigh, texDisplace - blendY );
				//half texBlend = 1.0 - smoothstep( 0.25, 0.75, abs( localNormalV.y ) );

				texDiffuse = lerp( texDiffuseY, texDiffuse, texBlend );
				worldNormal = normalize( lerp( worldNormalY, worldNormal, texBlend ) );
				texMetallic = lerp( texMetallicY, texMetallic, texBlend );
				texSmoothness = lerp( texSmoothnessY, texSmoothness, texBlend );
				texAO = lerp( texAOY, texAO, texBlend );
				texEdge = lerp( texEdgeY, texEdge, texBlend );

			#endif

			// Convert the world normal to tangent normal
			float3 tangentNormal = 0;
			tangentNormal.x = dot( worldVertTangent, worldNormal );
			tangentNormal.y = dot( worldVertBinormal, worldNormal );
			tangentNormal.z = dot( worldVertNormal, worldNormal );

			texDiffuse.xyz *= ( texEdge - 0.5 ) * _EdgePower + 1.0;
			texDiffuse.xyz = saturate( texDiffuse.xyz );
			texAO = saturate( texAO * ( texEdge + 0.5 ) );

			o.Albedo = texDiffuse.xyz;//saturate( pow( texDiffuse.rgb, _GamaCorrection ) );
			o.Normal = texNormal;
			half occlusion = pow( texAO, max( _AOPower, 0.001 ) );
			o.Metal = saturate( _Metallic * texMetallic.x );
			o.Gloss = saturate( _Smoothness * texSmoothness );
			o.SpecMask = o.Gloss;
			o.Occlusion = occlusion;
			o.Alpha = 1.0;
			// Skyrim has no AO map: the exported diffuse carries it, so the preview does too.
			o.Albedo = texDiffuse.xyz * occlusion;
			half3 ambIBL = texCUBElod(_ProbeCubemap, half4( normalize( worldNormal ), 7 ) ).xyz;
			// The environment as the game's ambient light: strong enough that a face turned away from the sun is
			// in shade, not black (the light now follows the HDRI's sun, so it is often behind the material).
			o.Ambient = ambIBL * 0.9;
			o.Emission = 0;

			float cut = IN.uv_DiffuseMap.x;
			if ( _AdjustReveal > 0.0 && cut < _AdjustReveal ) {
				half3 m = tex2D( _AdjustRevealMap, UV ).rgb;
				if ( _AdjustRevealInvert > 0.5 ) m = 1.0 - m;
				o.Albedo = 0; o.SpecMask = 0; o.Metal = 0; o.Occlusion = 1;
				o.Emission = m;
			}
			if ( _AdjustReveal > 0.0 && _AdjustReveal < 1.0 && abs( cut - _AdjustReveal ) < 0.0015 ) {
				o.Albedo = 0; o.SpecMask = 0; o.Metal = 0;
				o.Emission = half3( 0.24, 0.55, 0.99 );
			}
		}

		void skyrimFinal( Input IN, SurfaceOutputSkyrim o, inout fixed4 color )
		{
			// The revealed map is shown as it is, without the tone curve.
			if ( _SkyrimTone > 0.5 && !( _AdjustReveal > 0.0 && IN.uv_DiffuseMap.x < _AdjustReveal ) ) color.rgb = Hable( max( color.rgb, 0 ) ) / Hable( half3( 1, 1, 1 ) );
		}
		ENDCG
	}
	FallBack "Diffuse"
}
