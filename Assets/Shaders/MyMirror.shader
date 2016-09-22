// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "FX/MyMirror" {
Properties {
	[HideInInspector] _ReflectionTex ("Internal Reflection", 2D) = "" {}

}

Subshader {
	Tags { "RenderType"="Opaque" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog

#include "UnityCG.cginc"

struct appdata {
	float4 vertex : POSITION;
};

struct v2f {
	float4 pos : SV_POSITION;
	float4 ref : TEXCOORD2;
	UNITY_FOG_COORDS(4)
};

v2f vert(appdata v)
{
	v2f o;
	o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
	o.ref = ComputeScreenPos(o.pos);
	UNITY_TRANSFER_FOG(o,o.pos);
	return o;
}

sampler2D _ReflectionTex;

half4 frag( v2f i ) : SV_Target
{
	float4 uv1 = i.ref; 
	half4 refl = tex2Dproj( _ReflectionTex, UNITY_PROJ_COORD(uv1) );
	
	half4 color = refl;

	UNITY_APPLY_FOG(i.fogCoord, color);
	return color;
}
ENDCG

	}
}

}
