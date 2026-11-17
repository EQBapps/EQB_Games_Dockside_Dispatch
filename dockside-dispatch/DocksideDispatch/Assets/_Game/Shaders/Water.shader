// Stylised harbour water for Dockside Dispatch (URP, unlit, opaque).
// Everything is procedural in world XZ, so it needs no textures:
//  - depth tint from the quay (shallow) out to the horizon (picks up the sunset sky)
//  - a slow current: domain-warped value noise that drifts along +x
//  - broad swells plus two layers of stretched highlight streaks moving at different speeds
//  - a sun-glitter path under the setting sun
//  - foam lapping against the quay wall, and a second lapping line just off it
// Edges are anti-aliased with fwidth so the streaks stay crisp at any resolution.
Shader "Dockside/Water"
{
    Properties
    {
        _Shallow ("Shallow (near quay)", Color) = (0.18, 0.64, 0.62, 1)
        _Deep ("Deep", Color) = (0.11, 0.50, 0.53, 1)
        _Horizon ("Horizon tint", Color) = (0.95, 0.70, 0.56, 1)
        _Highlight ("Highlight streaks", Color) = (0.65, 0.89, 0.85, 1)
        _Glitter ("Sun glitter", Color) = (1.0, 0.90, 0.64, 1)
        _Foam ("Foam", Color) = (0.95, 0.98, 0.96, 1)
        // x = near z (quay wall), y = far z (horizon), z = sun x, w = current speed
        _Params ("NearZ, FarZ, SunX, Speed", Vector) = (2.6, 10, 1.6, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "WaterUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Horizon, _Highlight, _Glitter, _Foam;
                float4 _Params;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            // Anti-aliased threshold.
            float aastep (float threshold, float value)
            {
                float w = max(fwidth(value) * 0.75, 1e-4);
                return smoothstep(threshold - w, threshold + w, value);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _Params.w;
                float2 p = i.positionWS.xz;
                float nearZ = _Params.x, farZ = _Params.y;
                float d = saturate((p.y - nearZ) / (farZ - nearZ));   // 0 at the quay, 1 at the horizon

                half3 col = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0.0, 0.55, d));
                col = lerp(col, _Horizon.rgb, smoothstep(0.55, 1.0, d) * 0.75);

                // Slow current: warp the domain with drifting noise so everything flows gently along +x.
                float2 warp = float2(vnoise(p * 0.32 + float2(t * 0.045, 0.0)),
                                     vnoise(p * 0.32 + float2(5.7, t * 0.035)));
                float2 q = p + (warp - 0.5) * float2(1.2, 0.35) + float2(t * 0.12, 0.0);

                // Broad swells: soft light and dark bands across the harbour.
                float swell = vnoise(float2(q.x * 0.22, q.y * 1.15 - t * 0.05));
                col *= lerp(0.9, 1.07, swell);
                float swellLine = aastep(0.66, vnoise(float2(q.x * 0.35 + t * 0.03, q.y * 2.6)));
                col = lerp(col, col * 0.95, swellLine * 0.5);

                // Highlight streaks, stretched along x; perspective squash makes them read as ripples.
                float s1 = vnoise(float2(q.x * 0.9 + t * 0.18, q.y * 7.0));
                float s2 = vnoise(float2(q.x * 1.6 - t * 0.1, q.y * 11.0 + 3.1));
                float streak = aastep(0.8, s1) + aastep(0.84, s2) * 0.6;
                col = lerp(col, _Highlight.rgb, saturate(streak) * lerp(0.75, 0.45, d));

                // Sun glitter path, widening towards the horizon.
                float band = exp(-pow((p.x - _Params.z) / (0.3 + d * 0.7), 2.0)) * smoothstep(0.15, 0.85, d);
                float g = vnoise(float2(p.x * 5.0 + t * 0.5, p.y * 18.0 - t * 0.3));
                col = lerp(col, _Glitter.rgb, aastep(0.74 - band * 0.1, g) * band * 0.85);

                // Foam against the quay wall plus a lapping line that breathes in and out.
                float edge = p.y - nearZ;
                float foamN = vnoise(float2(p.x * 2.2 + t * 0.35, t * 0.25));
                float foam = 1.0 - aastep(0.07 + foamN * 0.16, edge);
                float lapPos = 0.32 + 0.07 * sin(p.x * 1.6 + t * 1.2) + 0.05 * sin(t * 0.8);
                float lap = (1.0 - aastep(0.022, abs(edge - lapPos))) * aastep(0.35, vnoise(float2(p.x * 1.4 - t * 0.2, 2.0)));
                col = lerp(col, _Foam.rgb, saturate(foam + lap * 0.75));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
