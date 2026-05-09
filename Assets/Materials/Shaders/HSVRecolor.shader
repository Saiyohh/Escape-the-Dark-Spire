Shader "Custom/HSVRecolor"
{
    // Exact port of STS2's hsv.gdshader
    // Uses RGB->YIQ color space for hue rotation, then saturation/value scaling.
    // Apply to UI Images (card frames, banners, borders) via Material.
    //
    // Usage:
    //   H (0-1): Hue rotation. 0 = no shift. Maps to 0-360 degrees internally.
    //   S (0-5): Saturation multiplier. 1 = unchanged, 0 = greyscale, >1 = oversaturated.
    //   V (0+):  Value/brightness multiplier. 1 = unchanged, 0 = black, >1 = brighter.
    //
    // STS2 Reference Values:
    //   Ironclad frame:   H=0.025 S=0.85  V=1.0   (Red)
    //   Silent frame:     H=0.32  S=0.45  V=1.2   (Green)
    //   Defect frame:     H=0.55  S=0.90  V=1.0   (Blue)
    //   Necrobinder frame:H=0.965 S=0.55  V=1.2   (Pink)
    //   Regent frame:     H=0.12  S=1.50  V=1.2   (Orange)
    //   Colorless frame:  H=1.0   S=0.0   V=1.2   (Grey)
    //
    //   Common banner:    H=1.0   S=0.0   V=0.85  (Grey)
    //   Uncommon banner:  H=1.0   S=1.0   V=1.0   (Blue - no shift)
    //   Rare banner:      H=0.563 S=1.198 V=1.14  (Gold)

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _H ("Hue", Range(0, 1)) = 1.0
        _S ("Saturation", Range(0, 5)) = 1.0
        _V ("Value", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _H;
                float _S;
                float _V;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color; // Vertex color = Unity's Image tint / modulate
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // RGB -> YIQ (same matrix as STS2)
                float3x3 RGB_to_YIQ = float3x3(
                    0.2989,  0.5959,  0.2115,
                    0.5870, -0.2774, -0.5229,
                    0.1140, -0.3216,  0.3114
                );

                float3x3 YIQ_to_RGB = float3x3(
                    1.0000,  1.0000,  1.0000,
                    0.9563, -0.2721, -1.1070,
                    0.6210, -0.6474,  1.7046
                );

                // Convert to YIQ
                float3 yiq = mul(RGB_to_YIQ, col.rgb);

                // Hue rotation (STS2 inverts: hue = 1.0 - h, then maps to 0..2PI)
                float hue = (1.0 - _H) * 6.283185;
                float sinH = sin(hue);
                float cosH = cos(hue);

                float3x3 hueShift = float3x3(
                    1.0,  0.0,    0.0,
                    0.0,  cosH,  -sinH,
                    0.0,  sinH,   cosH
                );
                yiq = mul(hueShift, yiq);

                // Saturation scaling
                float3x3 satShift = float3x3(
                    1.0, 0.0, 0.0,
                    0.0, _S,  0.0,
                    0.0, 0.0, _S
                );
                yiq = mul(satShift, yiq);

                // Value/brightness scaling
                yiq = lerp(float3(0, 0, 0), yiq, _V);

                // Convert back to RGB
                col.rgb = mul(YIQ_to_RGB, yiq);

                // Apply vertex color (Unity UI tint / modulate)
                col *= i.color;

                return col;
            }
            ENDHLSL
        }
    }
}
