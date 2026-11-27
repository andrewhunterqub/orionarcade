// Tilt-shift for the diorama look (visual brief section 5): blur grows towards the top and bottom of the
// screen, never the sides, around a sharp horizontal band.
Shader "Hidden/LoabTiltShift"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BlurTex;
    float4 _Params;   // x = focus (0..1 screen y), y = sharp half-band, z = softness, w = strength
    float4 _Dir;      // xy = blur direction in texels

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    fixed4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _Dir.xy * _MainTex_TexelSize.xy;
        fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
        c += tex2D(_MainTex, i.uv + d * 1.384615) * 0.316216;
        c += tex2D(_MainTex, i.uv - d * 1.384615) * 0.316216;
        c += tex2D(_MainTex, i.uv + d * 3.230769) * 0.070270;
        c += tex2D(_MainTex, i.uv - d * 3.230769) * 0.070270;
        return c;
    }

    fixed4 fragComposite (v2f i) : SV_Target
    {
        fixed4 sharp = tex2D(_MainTex, i.uv);
        fixed4 blur = tex2D(_BlurTex, i.uv);
        float dist = abs(i.uv.y - _Params.x);
        float m = smoothstep(_Params.y, _Params.y + _Params.z, dist) * _Params.w;
        return lerp(sharp, blur, m);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
    Fallback Off
}
