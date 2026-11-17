// Flat-shaded look for Dockside Dispatch (URP, unlit, opaque), matching the 2D art direction:
// every face shows the exact palette colour, shaded only by which way it faces
// (top = full colour, the camera-facing side = the darker "lip", ends in between).
// No lighting or shadows, so colours never go muddy, and it is cheap on phones.
// Optional texture (detail map, multiplied) and alpha cut-out (_Cutoff < 0 disables it).
Shader "Dockside/Flat"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _BaseMap ("Detail (multiplied)", 2D) = "white" {}
        // x = top, y = front (-Z, towards the camera), z = left/right ends, w = back / underside
        _Shade ("Face shading", Vector) = (1, 0.76, 0.87, 0.9)
        _Cutoff ("Alpha cut-off (-1 = off)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Flat"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Shade;
                half _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half shade : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                float3 n = normalize(TransformObjectToWorldNormal(i.normalOS));
                float top = saturate(n.y), front = saturate(-n.z), ends = abs(n.x), back = saturate(n.z) + saturate(-n.y);
                float sum = max(top + front + ends + back, 1e-3);
                o.shade = (top * _Shade.x + front * _Shade.y + ends * _Shade.z + back * _Shade.w) / sum;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                return half4(_BaseColor.rgb * tex.rgb * i.shade, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
