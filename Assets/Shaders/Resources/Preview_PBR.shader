// Materialize CE: the modern PBR preview. Lighting computed in linear light with a GGX BRDF and image-based
// light from the environment (see the lighting section), plus:
//  - specular occlusion: reflections fade in crevices (from the AO and the view angle, Lagarde) and where a
//    normal-mapped reflection would point under the surface (horizon occlusion);
//  - contact shadows: direct light shaded by the AO in crevices (micro-shadowing, Chan) and small shadows cast by
//    the height map itself, marched towards the light (finer than the shadow map);
//  - fine relief: parallax occlusion on top of the tessellation, for the texel-sized detail it cannot reach;
//  - specular anti-aliasing: roughness raised where the normal changes faster than a pixel (no shimmering).
// Same tessellated displacement, top projection and reveal slider as the previous PBR preview. Each improvement
// has its own strength (Material & lighting > Rendering quality).
Shader "Custom/Preview_PBR" {
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
		_DispOffset ("Displacement Offset", Float ) = 1.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf PreviewPBR vertex:vert fullforwardshadows addshadow noforwardadd nometa nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass tessellate:tessEdge
		#pragma target 5.0
		#pragma multi_compile _ TOP_PROJECTION
		#include "UnityCG.cginc"
		#include "Tessellation.cginc"
		#include "UnityPBSLighting.cginc"

		sampler2D _DiffuseMap;
		sampler2D _NormalMap;
		sampler2D _SmoothnessMap;
		sampler2D _MetallicMap;
		sampler2D _AOMap;
		sampler2D _EdgeMap;
		sampler2D _DisplacementMap;
		float4 _DisplacementMap_TexelSize;

		float4 _Tiling;
		float4 _Tint;
		float _AOPower;
		float _EdgePower;
		float _Smoothness;
		float _Metallic;
		float _Dust;
		int _FlipNormalY;
		float _Parallax;
		float _DispOffset;
		float _EdgeLength;

		// Rendering quality (globals, set from Material & lighting). 0 = off, 1 = full.
		uniform float _QualitySpecOcclusion;
		uniform float _QualityMicroShadow;
		uniform float _QualitySelfShadow;
		uniform float _QualityFineRelief;
		uniform float _QualitySpecAA;
		uniform float _QualitySet;          // 0 until the settings are sent: then the defaults below apply
		uniform float _CsLook;              // 1: lit as Skyrim's Community Shaders light a True PBR material
		// Emission and subsurface colour maps (globals: black when the material has none).
		uniform sampler2D _MceEmissionMap;
		uniform sampler2D _MceSubsurfaceMap;
		uniform float _MceEmissionStrength;
		uniform float _CsExposure;          // the game's light level (sun, ambient, eye adaptation) against the preview's

		// Adjust's reveal slider.
		uniform float _AdjustReveal;
		uniform float _AdjustRevealInvert;
		uniform sampler2D _AdjustRevealMap;


		samplerCUBE _ProbeCubemap;
		uniform float _ProbeMaxMip;   // last mip of the reflection probe (log2 of its size)

		float Q(float v, float def) { return _QualitySet > 0.5 ? v : def; }

		struct SurfaceOutputPreview {
			fixed3 Albedo;
			float3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			fixed Alpha;
			half SelfShadow;       // height-map shadow towards the light, 1 = lit
			float3 VertexNormal;   // the surface's own normal, for the horizon occlusion
			half Revealed;         // 1: Adjust's reveal shows the map here, unlit
			half3 Subsurface;      // colour of the light going through the material (black: opaque)
		};

		SurfaceOutputStandard ToStandard(SurfaceOutputPreview s)
		{
			SurfaceOutputStandard o;
			o.Albedo = s.Albedo; o.Normal = s.Normal; o.Emission = s.Emission; o.Metallic = s.Metallic;
			o.Smoothness = s.Smoothness; o.Occlusion = s.Occlusion; o.Alpha = s.Alpha;
			return o;
		}

		// ---------- Linear, physically based lighting (Materialize CE) ----------
		// The project renders in gamma space: colours are turned into linear light here, lit with a GGX BRDF
		// (4 % dielectric reflectance, not the 22 % Unity uses for gamma projects), and encoded back. The
		// post-process ACES tone mapping then works on proper HDR values.

		half3 ToLinear( half3 c ) { return pow( max( c, 0 ), 2.2 ); }
		half3 ToGamma( half3 c ) { return pow( max( c, 0 ), 1.0 / 2.2 ); }

		// Split-sum environment BRDF, analytic fit (Karis, "Physically Based Shading on Mobile").
		half2 EnvBRDF( half roughness, half nv )
		{
			const half4 c0 = half4( -1, -0.0275, -0.572, 0.022 );
			const half4 c1 = half4( 1, 0.0425, 1.04, -0.04 );
			half4 r = roughness * c0 + c1;
			half a004 = min( r.x * r.x, exp2( -9.28 * nv ) ) * r.x + r.y;
			return half2( -1.04, 1.04 ) * a004 + r.zw;
		}

		half4 LightingPreviewPBR (SurfaceOutputPreview s, half3 viewDir, UnityGI gi)
		{
			if ( s.Revealed > 0.5 ) return half4( 0, 0, 0, 1 );   // the map alone, through the emission
			half3 N = normalize( s.Normal );
			half3 V = viewDir;
			half3 L = gi.light.dir;
			half3 H = normalize( L + V );
			half nl = saturate( dot( N, L ) );
			half nv = max( dot( N, V ), 1e-4 );
			half nh = saturate( dot( N, H ) );
			half lh = saturate( dot( L, H ) );

			half3 albedo = ToLinear( s.Albedo );
			half metal = s.Metallic;
			half3 diffColor = albedo * ( 1.0 - metal );
			half3 f0 = lerp( half3( 0.04, 0.04, 0.04 ), albedo, metal );
			half perceptual = 1.0 - s.Smoothness;
			half a = max( perceptual * perceptual, 0.002 );
			half a2 = a * a;

			// Direct light: GGX distribution, height-correlated Smith visibility, Schlick Fresnel.
			half d = nh * nh * ( a2 - 1.0 ) + 1.0;
			half D = a2 / ( UNITY_PI * d * d + 1e-7 );
			half lambdaV = nl * sqrt( nv * nv * ( 1.0 - a2 ) + a2 );
			half lambdaL = nv * sqrt( nl * nl * ( 1.0 - a2 ) + a2 );
			half Vis = 0.5 / ( lambdaV + lambdaL + 1e-5 );
			half3 F = f0 + ( 1.0 - f0 ) * pow( 1.0 - lh, 5.0 );

			// Rough surfaces lose energy with single scattering: compensated (multiple scattering).
			half2 ab = EnvBRDF( perceptual, nv );
			half3 envSpec = f0 * ab.x + ab.y;
			half3 energy = 1.0 + f0 * ( 1.0 / max( ab.x + ab.y, 0.05 ) - 1.0 );

			// Micro-shadowing and relief shadows on the direct light only.
			half micro = saturate( abs( dot( N, L ) ) + 2.0 * s.Occlusion * s.Occlusion - 1.0 );
			half3 light = ToLinear( gi.light.color ) * lerp( 1.0, micro, Q( _QualityMicroShadow, 0.0 ) ) * s.SelfShadow;

			// The highlight is capped (it is white after tone mapping anyway): no single-pixel sparks.
			half3 specDirect = min( D * Vis * F * energy * nl * UNITY_PI, 2.0 );
			half3 direct = ( diffColor * nl + specDirect ) * light;

			// Image-based light: diffuse from the environment (a strongly blurred sample of the probe around the
			// normal), specular from the probe at the roughness' blur, both occluded.
			half3 irradiance = ToLinear( texCUBElod( _ProbeCubemap, half4( N, 6.5 ) ).rgb );
			half3 diffuseIBL = diffColor * irradiance * s.Occlusion;
			// Reflection blurred over the probe's whole mip chain by the roughness (Unity's own lookup stops at
			// mip 6, still sharp on a 1024 probe: rough surfaces then reflected like varnish, a plastic look).
			half3 R0 = reflect( -V, N );
			half maxMip = _ProbeMaxMip > 0.5 ? _ProbeMaxMip : 10.0;
			half lod = perceptual * ( 1.7 - 0.7 * perceptual ) * maxMip;
			half3 prefiltered = ToLinear( texCUBElod( _ProbeCubemap, half4( R0, lod ) ).rgb );
			half specOcc = saturate( pow( nv + s.Occlusion, exp2( -16.0 * perceptual - 1.0 ) ) - 1.0 + s.Occlusion );
			half3 R = reflect( -V, N );
			half horizon = saturate( 1.0 + 1.3 * dot( R, s.VertexNormal ) );
			half occ = lerp( s.Occlusion, specOcc * horizon * horizon, Q( _QualitySpecOcclusion, 1.0 ) );
			half3 specularIBL = prefiltered * envSpec * energy * occ;

			// Subsurface (thin: leaves, wax): light let through, stronger when looking towards the light. The shape
			// of Community Shaders' term (PBR.hlsli), with no thickness map.
			half forwardScatter = exp2( saturate( -dot( V, L ) ) * 12.234 - 12.234 );
			half throughAmount = lerp( 0.5, 1.0, forwardScatter );
			direct += ToLinear( s.Subsurface ) * throughAmount * light * ( 1.0 - F );

			if ( _CsLook > 0.5 ) {
				// Community Shaders, True PBR without linear lighting (Lighting.hlsl, PBR.hlsli, Color.hlsli):
				// the base colour and the game's light and ambient values are used as they are (gamma), the
				// diffuse sum is scaled by 0.65 (PBRLightingScale), raised to 1.6 ("Skyrim gamma"), the specular
				// and the cubemap reflection are added there, and the result goes back through 1/1.6.
				half3 baseG = s.Albedo * ( 1.0 - metal );
				half3 f0G = lerp( half3( 0.04, 0.04, 0.04 ), s.Albedo, metal );
				half3 Fg = f0G + ( 1.0 - f0G ) * pow( 1.0 - lh, 5.0 );
				half gameExposure = _CsExposure > 0.0 ? _CsExposure : 1.0;
				half3 lightG = gi.light.color * lerp( 1.0, micro, Q( _QualityMicroShadow, 0.0 ) ) * s.SelfShadow * gameExposure;
				half3 envSpecG = f0G * ab.x + ab.y;
				// The environment stands for the game's ambient light (its directional ambient).
				half3 ambientG = texCUBElod( _ProbeCubemap, half4( N, 6.5 ) ).rgb * gameExposure;
				half3 diffuseG = baseG * ( lightG * nl * ( 1.0 - Fg ) + ( 1.0 - envSpecG ) * s.Occlusion * ambientG ) * 0.65;
				half3 lin = pow( max( diffuseG, 0 ), 1.6 );
				lin += min( D * Vis * Fg * nl * UNITY_PI, 2.0 ) * lightG * 0.65;
				half3 reflG = pow( max( texCUBElod( _ProbeCubemap, half4( R0, lod ) ).rgb * gameExposure, 0 ), 1.6 );
				lin += envSpecG * occ * reflG * 0.65;
				lin += pow( max( s.Subsurface, 0 ), 1.6 ) * throughAmount * lightG * ( 1.0 - Fg ) * 0.65;
				return half4( pow( max( lin, 0 ), 1.0 / 1.6 ), 1.0 );
			}

			return half4( ToGamma( direct + diffuseIBL + specularIBL ), 1.0 );
		}

		void LightingPreviewPBR_GI (SurfaceOutputPreview s, UnityGIInput data, inout UnityGI gi)
		{
			// Only the reflection probe's sample is used (at the blur of the roughness); ambient comes from the probe.
			Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup( s.Smoothness, data.worldViewDir, s.Normal, half3( 0.04, 0.04, 0.04 ) );
			gi = UnityGlobalIllumination( data, 1.0, s.Normal, g );
		}

		float4 tessEdge (appdata_full v0, appdata_full v1, appdata_full v2 )
		{
			return UnityEdgeLengthBasedTess(v0.vertex, v1.vertex, v2.vertex, _EdgeLength );
		}

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
			if ( !( _AdjustReveal > 0.0 && v.texcoord.x < _AdjustReveal ) ) v.vertex.xyz += v.normal * d;   // the revealed map stays flat
		}

		UNITY_INSTANCING_BUFFER_START(Props)
		UNITY_INSTANCING_BUFFER_END(Props)

		float Height(float2 uv, float lod) { return tex2Dlod( _DisplacementMap, float4( uv, 0, lod ) ).x; }

		void surf (Input IN, inout SurfaceOutputPreview o) {

			float2 UV = IN.uv_DiffuseMap.xy * _Tiling.xy + _Tiling.zw;

			float3 worldVertTangent = WorldNormalVector (IN, float3(1,0,0) );
			float3 worldVertBinormal = WorldNormalVector (IN, float3(0,1,0) );
			float3 worldVertNormal = WorldNormalVector (IN, float3(0,0,1) );

			// World size of one texture unit, and of the full height, for the parallax effects below.
			float objScale = length( unity_ObjectToWorld[0].xyz );
			float2 duvx = ddx( UV ), duvy = ddy( UV );
			float worldPerUV = max( length( ddx( IN.worldPos ) ), length( ddy( IN.worldPos ) ) ) / max( max( length( duvx ), length( duvy ) ), 1e-6 );
			float heightWorld = _Parallax * ( 1.0 / _Tiling.x ) * objScale;
			float heightUV = heightWorld / max( worldPerUV, 1e-5 );   // full height, in texture units
			float texLod = max( 0.0, 0.5 * log2( max( dot( duvx, duvx ), dot( duvy, duvy ) ) * _DisplacementMap_TexelSize.z * _DisplacementMap_TexelSize.w ) );

			#ifndef TOP_PROJECTION
			// Fine relief: parallax occlusion of the detail the tessellation cannot follow. The tessellated mesh
			// already carries the large shapes (roughly the height map blurred to its triangle size): only what is
			// left, the fine detail around it, is traced here.
			float fine = Q( _QualityFineRelief, 0.0 );   // off by default: can smear on steep displaced slopes
			if ( fine > 0.001 && heightUV > 1e-5 ) {
				float3 v = normalize( IN.viewDir );   // tangent space
				float coarseLod = texLod + log2( max( _EdgeLength, 1.0 ) ) + 1.5;
				float hCoarse = Height( UV, coarseLod );
				// Only where the tessellated surface is fairly flat: on its slopes the tangent frame is still the
				// flat mesh's, so the parallax would shift the wrong way.
				float gstep = _DisplacementMap_TexelSize.x * exp2( coarseLod );
				float2 grad = float2( Height( UV + float2( gstep, 0 ), coarseLod ) - Height( UV - float2( gstep, 0 ), coarseLod ),
				                      Height( UV + float2( 0, gstep ), coarseLod ) - Height( UV - float2( 0, gstep ), coarseLod ) ) / ( 2.0 * gstep );
				float slope = length( grad ) * heightUV;   // rise over run of the tessellated surface
				fine *= saturate( 1.0 - slope * 2.5 );
				// Fine detail only shows up close: faded out once a pixel covers several texels.
				fine *= saturate( 1.0 - texLod / 2.5 );
				float2 stepUV = -v.xy / max( v.z, 0.2 ) * heightUV * fine;
				const int STEPS = 24;
				float layer = 1.0 / STEPS;
				float2 cur = UV, prevUV = UV;
				float rayH = 1.0, prevRay = 1.0, prevMap = 1.0, mapH = 1.0;
				[loop]
				for ( int i = 0; i <= STEPS; i++ ) {
					mapH = saturate( 0.5 + ( Height( cur, texLod ) - hCoarse ) );
					if ( mapH >= rayH ) break;
					prevUV = cur; prevRay = rayH; prevMap = mapH;
					rayH -= layer;
					cur += stepUV * layer;
				}
				// Linear refinement between the last two steps.
				float before = prevRay - prevMap, after = mapH - rayH;
				float t = saturate( before / max( before + after, 1e-5 ) );
				// Centred: the average detail sits at mid height, so the texture does not slide as a whole.
				float2 offset = lerp( prevUV, cur, t ) - stepUV * 0.5 - UV;
				// Only fine detail: at most a few texels (on steep slopes a larger shift would smear the texture).
				float maxOffset = 2.0 * _DisplacementMap_TexelSize.x;
				float len = length( offset );
				if ( len > maxOffset ) offset *= maxOffset / len;
				UV += offset;
			}
			#endif

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
			// Toksvig: averaged (far away) normals are shorter; that spread becomes roughness instead of sparkles.
			float normalLen = clamp( length( texNormal ), 0.05, 1.0 );
			float toksvig = min( ( 1.0 - normalLen ) / normalLen, 0.5 );

			fixed3 worldNormal = WorldNormalVector (IN, texNormal );

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
				float3 worldNormalY = ( texNormalY.x * tangentY ) + ( texNormalY.y * binormalY ) + ( texNormalY.z * worldVertNormal );
				half blendY = texDisplaceY + ( abs( localNormalV.y * 3.0 ) - 1.5 );
				half blendFalloff = 0.1;
				half SSHigh = 0.01 + ( saturate( blendFalloff ) );
				half SSLow = -0.01 - ( saturate( blendFalloff ) );
				half texBlend = smoothstep( SSLow, SSHigh, texDisplace - blendY );
				texDiffuse = lerp( texDiffuseY, texDiffuse, texBlend );
				worldNormal = normalize( lerp( worldNormalY, worldNormal, texBlend ) );
				texMetallic = lerp( texMetallicY, texMetallic, texBlend );
				texSmoothness = lerp( texSmoothnessY, texSmoothness, texBlend );
				texAO = lerp( texAOY, texAO, texBlend );
				texEdge = lerp( texEdgeY, texEdge, texBlend );
			#endif

			float3 tangentNormal;
			tangentNormal.x = dot( worldVertTangent, worldNormal );
			tangentNormal.y = dot( worldVertBinormal, worldNormal );
			tangentNormal.z = dot( worldVertNormal, worldNormal );

			texDiffuse.xyz *= ( texEdge - 0.5 ) * _EdgePower + 1.0;
			texDiffuse.xyz = saturate( texDiffuse.xyz );
			texAO = saturate( texAO * ( texEdge + 0.5 ) );

			o.Albedo = texDiffuse.xyz;
			o.Normal = normalize( tangentNormal );
			o.Metallic = saturate( _Metallic * texMetallic.x );
			half smooth = saturate( _Smoothness * texSmoothness );

			// Specular anti-aliasing (Kaplanyan / Tokuyasu): where the normal varies within a pixel, the highlight
			// is widened to what the pixel really covers, instead of flickering.
			// The spread of the normals is read from the normal map one mip up (how much shorter the averaged
			// normal is): smooth from pixel to pixel, where screen derivatives come in 2x2 blocks.
			float3 wide = tex2Dbias( _NormalMap, float4( UV, 0, 1.0 ) ).xyz * 2.0 - 1.0;
			float wideLen = clamp( length( wide ), 0.05, 1.0 );
			float variance = ( 1.0 - wideLen ) / wideLen;
			// Up to nearly rough: at the rim of a glossy puddle against rough ground, single pixels would mirror the sun.
			float kernel = min( 2.0 * variance * Q( _QualitySpecAA, 1.0 ), 0.3 );
			float alpha = ( 1.0 - smooth ) * ( 1.0 - smooth );
			float a2 = saturate( alpha * alpha + kernel + toksvig * Q( _QualitySpecAA, 1.0 ) );
			o.Smoothness = 1.0 - sqrt( sqrt( a2 ) );

			o.Occlusion = pow( texAO, max( _AOPower, 0.001 ) );
			o.Alpha = 1.0;
			o.Revealed = 0;
			o.VertexNormal = worldVertNormal;
			// The environment's light is part of the lighting (image-based); the emission is the material's own.
			o.Emission = tex2D( _MceEmissionMap, UV ).rgb * _MceEmissionStrength;
			o.Subsurface = tex2D( _MceSubsurfaceMap, UV ).rgb;

			// Height-map shadows towards the light: fine contact shadows the shadow map is too coarse for.
			o.SelfShadow = 1.0;
			#ifndef TOP_PROJECTION
			float selfAmount = Q( _QualitySelfShadow, 1.0 );
			float3 Lw = normalize( _WorldSpaceLightPos0.xyz );
			float3 Lt = float3( dot( Lw, worldVertTangent ), dot( Lw, worldVertBinormal ), dot( Lw, worldVertNormal ) );
			if ( selfAmount > 0.001 && Lt.z > 0.02 && heightUV > 1e-5 ) {
				float h0 = Height( UV, texLod );
				// Rise from the surface towards the light, until above the highest point.
				float2 dir = Lt.xy / Lt.z * heightUV;
				const int SHADOW_STEPS = 20;
				float blocked = 0.0;
				[loop]
				for ( int k = 1; k <= SHADOW_STEPS; k++ ) {
					float t = k / (float)SHADOW_STEPS;
					float rayH = h0 + t * ( 1.0 - h0 );
					float hk = Height( UV + dir * ( t * ( 1.0 - h0 ) ), texLod );
					// Soft: how far the terrain rises above the ray, weighted towards the start.
					blocked = max( blocked, ( hk - rayH ) * ( 1.0 - t ) * 12.0 );
				}
				o.SelfShadow = lerp( 1.0, 1.0 - saturate( blocked ), selfAmount );
			}
			#endif

			float cut = IN.uv_DiffuseMap.x;
			if ( _AdjustReveal > 0.0 && cut < _AdjustReveal ) {
				half3 m = tex2D( _AdjustRevealMap, UV ).rgb;
				if ( _AdjustRevealInvert > 0.5 ) m = 1.0 - m;
				o.Albedo = 0; o.Metallic = 0; o.Smoothness = 0; o.Occlusion = 1; o.SelfShadow = 1; o.Revealed = 1;
				o.Emission = m;
			}
			if ( _AdjustReveal > 0.0 && _AdjustReveal < 1.0 && abs( cut - _AdjustReveal ) < 0.0015 ) {
				o.Albedo = 0; o.Metallic = 0; o.Smoothness = 0;
				o.Emission = half3( 0.24, 0.55, 0.99 );
			}
		}
		ENDCG
	}
	FallBack "Custom/SurfacePBS_Tess_Generated"
}
