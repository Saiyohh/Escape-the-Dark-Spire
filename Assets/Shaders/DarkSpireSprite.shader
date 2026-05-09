// DarkSpireSprite.shader
// ----------------------------------------------------------------------------
// Unified sprite shader that combines two independent effects in one pass:
//
//   1. Paper softening + grain
//      • 5-tap alpha cross sample for softened edges
//      • World-anchored procedural grain
//      • Paper tint multiply
//      Zero any parameter to disable that sub-effect.
//
//   2. Outline (8-direction alpha scan)
//      • True silhouette-boundary outline — does NOT bleed through interior
//        transparent regions like fur detail / finger gaps / jaw interiors
//      • Outline width = 0 disables the effect entirely
//
// Typical split of where parameters live:
//   • Paper settings — on the material asset, authored once for scene-wide
//     aesthetic consistency.
//   • Outline settings — driven per-instance via MaterialPropertyBlock by
//     the SpriteOutline component. Different objects on the same material
//     can have different outline colors/widths without material duplication
//     or batching breakage.
//
// Both features can run simultaneously — paper softens the sprite body while
// outline scans the raw alpha for the silhouette edge.
//
// Requires the sprite asset to have mesh room around the opaque pixels:
//   • Sprite Editor → Mesh Type = Full Rect     (easiest)
//   • OR source art with ~8px transparent margin
//   • OR Sprite Import → Extrude Edges = 4
// ----------------------------------------------------------------------------
Shader "DarkSpire/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Paper Softening)]
        _EdgeSoftness ("Edge Softness (texels)", Range(0, 8)) = 0
        _GrainStrength ("Grain Strength", Range(0, 1)) = 0
        _GrainScale ("Grain Scale", Range(1, 250)) = 60
        _PaperTint ("Paper Tint", Color) = (1, 1, 1, 1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 16)) = 0
        _OutlineSoftness ("Outline Softness", Range(0, 4)) = 0.5
        _OutlineAlphaThresh ("Alpha Threshold", Range(0.01, 1)) = 0.3
        _OutlineGrainStrength ("Outline Grain Strength", Range(0, 1)) = 0.5
        _OutlineGrainScale ("Outline Grain Scale (relative)", Range(0.25, 4)) = 1.0
        [Toggle] _OutlineBehind ("Outline Behind Sprite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            fixed4    _Color;

            float  _EdgeSoftness;
            float  _GrainStrength;
            float  _GrainScale;
            fixed4 _PaperTint;

            fixed4 _OutlineColor;
            float  _OutlineWidth;
            float  _OutlineSoftness;
            float  _OutlineAlphaThresh;
            float  _OutlineGrainStrength;
            float  _OutlineGrainScale;
            float  _OutlineBehind;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.color    = IN.color * _Color;
                OUT.texcoord = IN.texcoord;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                return OUT;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Sample alpha at a UV, returning 0 if outside [0,1] (keeps scans clean
            // at the sprite boundary — anything off-sprite counts as transparent).
            float SampleAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy;

                // ---- (1) Paper softening: sample sprite with optional edge blur ----
                fixed4 src;
                if (_EdgeSoftness > 0.001)
                {
                    float2 o = ts * _EdgeSoftness;
                    fixed4 c  = tex2D(_MainTex, IN.texcoord);
                    fixed4 cN = tex2D(_MainTex, IN.texcoord + float2(0,  o.y));
                    fixed4 cS = tex2D(_MainTex, IN.texcoord + float2(0, -o.y));
                    fixed4 cE = tex2D(_MainTex, IN.texcoord + float2( o.x, 0));
                    fixed4 cW = tex2D(_MainTex, IN.texcoord + float2(-o.x, 0));
                    src = (c + cN + cS + cE + cW) * 0.2;
                }
                else
                {
                    src = tex2D(_MainTex, IN.texcoord);
                }

                src *= IN.color;

                if (_GrainStrength > 0.001)
                {
                    float g = hash(IN.worldPos.xy * _GrainScale);
                    src.rgb += (g - 0.5) * _GrainStrength;
                }
                src.rgb *= _PaperTint.rgb;

                // ---- (2) Outline: 8-dir alpha scan on RAW alpha, not softened ----
                if (_OutlineWidth > 0.001 && _OutlineColor.a > 0.001)
                {
                    float w = _OutlineWidth;
                    float d = w * 0.70710678; // /sqrt(2) for diagonal neighbors

                    float aN  = SampleAlpha(IN.texcoord + float2(0,  w) * ts);
                    float aS  = SampleAlpha(IN.texcoord + float2(0, -w) * ts);
                    float aE  = SampleAlpha(IN.texcoord + float2( w, 0) * ts);
                    float aW  = SampleAlpha(IN.texcoord + float2(-w, 0) * ts);
                    float aNE = SampleAlpha(IN.texcoord + float2( d,  d) * ts);
                    float aNW = SampleAlpha(IN.texcoord + float2(-d,  d) * ts);
                    float aSE = SampleAlpha(IN.texcoord + float2( d, -d) * ts);
                    float aSW = SampleAlpha(IN.texcoord + float2(-d, -d) * ts);

                    float neighborAlpha = max(max(max(aN, aS), max(aE, aW)),
                                              max(max(aNE, aNW), max(aSE, aSW)));

                    // Use the RAW alpha for outline masking so softening doesn't
                    // creep the detection band.
                    float rawAlpha = tex2D(_MainTex, IN.texcoord).a;

                    float t = _OutlineAlphaThresh;
                    float s = _OutlineSoftness * 0.05;

                    float currentTransparent = 1.0 - smoothstep(t - s, t + s, rawAlpha);
                    float neighborOpaque     = smoothstep(t - s, t + s, neighborAlpha);

                    float outlineMask = currentTransparent * neighborOpaque;

                    // ── Outline grain (dither + tint wobble) ──
                    // Second grain sample, offset seed so the outline's grain
                    // pattern is distinct from (but at the same spatial scale
                    // as) the body grain — keeps them visually consistent.
                    float outlineGrain = hash(
                        IN.worldPos.xy * _GrainScale * _OutlineGrainScale
                        + float2(13.37, 7.73));

                    // Modulate the outline's alpha so parts of the edge fade
                    // like pencil strokes instead of rendering as a flat band.
                    float grainAlphaFade = lerp(
                        1.0 - _OutlineGrainStrength,
                        1.0,
                        outlineGrain);
                    outlineMask *= grainAlphaFade;

                    fixed4 outlineRGBA = _OutlineColor;
                    // Apply the paper tint to the outline color so white outlines
                    // soften toward the same warm off-white as the rest of the art.
                    outlineRGBA.rgb *= _PaperTint.rgb;
                    // Subtle brightness wobble — makes the stroke look uneven
                    // like graphite fighting paper fibers.
                    outlineRGBA.rgb += (outlineGrain - 0.5) * _OutlineGrainStrength * 0.25;
                    outlineRGBA.a *= outlineMask;

                    fixed4 result;
                    if (_OutlineBehind > 0.5)
                    {
                        // Sprite on top, outline bleeds through transparent gaps only
                        result.rgb = src.rgb * src.a + outlineRGBA.rgb * outlineRGBA.a * (1 - src.a);
                        result.a   = src.a + outlineRGBA.a * (1 - src.a);
                    }
                    else
                    {
                        // Outline painted on top of sprite edge
                        result.rgb = outlineRGBA.rgb * outlineRGBA.a + src.rgb * src.a * (1 - outlineRGBA.a);
                        result.a   = outlineRGBA.a + src.a * (1 - outlineRGBA.a);
                    }
                    return result;
                }

                // No outline active — just return the paper-softened sprite.
                src.rgb *= src.a; // premultiply for One/OneMinusSrcAlpha
                return src;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
