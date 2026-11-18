// Soft additive light for Dockside Dispatch: a radial falloff from the centre of the mesh UVs (0..1), used for
// floodlight pools on the quay and lamp halos at night. Colour alpha scales the strength.
Shader "Dockside/Glow"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.85, 0.55, 0.5)
        _Falloff ("Falloff power", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Glow"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half r = saturate(length(i.uv - 0.5) * 2.0);
                half a = pow(1.0 - r, _Falloff) * _BaseColor.a;
                return half4(_BaseColor.rgb * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
