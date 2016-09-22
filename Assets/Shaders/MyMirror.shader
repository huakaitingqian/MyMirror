// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "FX/MyMirror" {
Properties {
	[NoScaleOffset] _BumpMap ("Normalmap ", 2D) = "bump" {}
	[HideInInspector] _ReflectionTex ("Internal Reflection", 2D) = "" {}

}


// -----------------------------------------------------------
// Fragment program cards


Subshader {
	Tags { "WaterMode"="Refractive" "RenderType"="Opaque" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog

#include "UnityCG.cginc"


uniform float _ReflDistort;

struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 uv_bump : TEXCOORD0;
	float4 uv_reflect : TEXCOORD1;
};

struct v2f {
	float4 pos : SV_POSITION;
	float2 uv_bump : TEXCOORD0;
	float4 uv_reflect : TEXCOORD1;
	float3 viewDir : TEXCOORD2;
	float4 ref : TEXCOORD3;
	UNITY_FOG_COORDS(4)
};

v2f vert(appdata v)
{
	v2f o;
	o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
	
	float4 wpos = mul (unity_ObjectToWorld, v.vertex);
	o.uv_bump = v.uv_bump;
	o.uv_reflect = v.uv_reflect;
	// object space view direction (will normalize per pixel)
	o.viewDir.xzy = WorldSpaceViewDir(v.vertex);

	o.ref = ComputeScreenPos(o.pos);

	UNITY_TRANSFER_FOG(o,o.pos);
	return o;
}


sampler2D _ReflectionTex;
sampler2D _ReflectiveColor;
sampler2D _BumpMap;

half4 frag( v2f i ) : SV_Target
{
	i.viewDir = normalize(i.viewDir);
	
	// combine two scrolling bumpmaps into one
	half3 bump = UnpackNormal(tex2D( _BumpMap, i.uv_bump )).rgb;
	
	// perturb reflection/refraction UVs by bumpmap, and lookup colors
	half4 refl = tex2Dproj( _ReflectionTex, UNITY_PROJ_COORD(i.uv_reflect) );
	
	// final color is between refracted and reflected based on fresnel
	half4 color;
	color.rgb = refl.rgb;
	color.a = refl.a;

	UNITY_APPLY_FOG(i.fogCoord, color);
	return color;
}
ENDCG

	}
}

}
