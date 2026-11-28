// Soft additive halo for glowing props (visual brief, task V3): a cheap stand-in for bloom on WebGL.
// Drawn on a Quad; the vertex shader turns it to face the camera, sized by the object's X scale.
// _HaloStrength is a global set by WorldView (brighter at night, zero on the lowest device tier).
Shader "LOAB/Halo"
{
    Properties { _Color ("Color", Color) = (1, 0.82, 0.48, 1) }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _HaloStrength;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v)
            {
                v2f o;
                float3 centre = UnityObjectToViewPos(float3(0, 0, 0));
                float s = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float3 vp = centre + float3(v.vertex.xy * s, 0.25);
                o.pos = mul(UNITY_MATRIX_P, float4(vp, 1));
                o.uv = v.vertex.xy * 2;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = saturate(1 - length(i.uv));
                return _Color * (d * d * _HaloStrength);
            }
            ENDCG
        }
    }
}
