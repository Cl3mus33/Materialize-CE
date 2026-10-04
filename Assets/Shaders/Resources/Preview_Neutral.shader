// Materialize CE: the parts of an imported mesh that do not show the material: a plain grey, lit by the scene light.
Shader "Hidden/Preview_Neutral" {
	SubShader {
		Tags { "RenderType" = "Opaque" }
		Pass {
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD0;
			};

			v2f vert (appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos( v.vertex );
				o.normal = UnityObjectToWorldNormal( v.normal );
				return o;
			}

			fixed4 frag (v2f IN) : SV_Target {
				float light = 0.45 + 0.55 * saturate( dot( normalize( IN.normal ), normalize( _WorldSpaceLightPos0.xyz ) ) );
				return fixed4( 0.33 * light, 0.34 * light, 0.36 * light, 1.0 );
			}
			ENDCG
		}
	}
}
