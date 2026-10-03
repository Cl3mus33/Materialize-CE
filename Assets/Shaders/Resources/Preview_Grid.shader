// Materialize CE: a reference grid at the preview plane's zero level (G). Lines every unit (the plane is 10 x 10)
// and finer ones every quarter, fading towards the edges. Depth-tested and not written: what the displacement
// raises hides it, what sinks shows through underneath.
Shader "Hidden/Preview_Grid" {
	Properties {
		_Color ("Color", Color) = (0.24, 0.55, 0.99, 0.55)
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Pass {
			ZWrite Off
			ZTest LEqual
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha
			Offset -1, -1

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			float4 _Color;

			struct v2f { float4 pos : SV_POSITION; float2 p : TEXCOORD0; };

			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos( v.vertex );
				o.p = v.vertex.xy * 20.0;   // the quad is scaled by 20: plane units
				return o;
			}

			float Line( float2 p, float spacing )
			{
				float2 q = p / spacing;
				float2 d = abs( frac( q - 0.5 ) - 0.5 ) / fwidth( q );
				return 1.0 - saturate( min( d.x, d.y ) );
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float major = Line( i.p, 1.0 );
				float minor = Line( i.p, 0.25 ) * 0.35;
				float axis = 1.0 - saturate( min( abs( i.p.x ), abs( i.p.y ) ) / fwidth( i.p.x ) );
				float a = max( max( major, minor ), axis );
				// Fades out beyond the plane (±5).
				float r = max( abs( i.p.x ), abs( i.p.y ) );
				a *= 1.0 - smoothstep( 5.5, 9.5, r );
				return fixed4( _Color.rgb, _Color.a * a );
			}
			ENDCG
		}
	}
	Fallback Off
}
