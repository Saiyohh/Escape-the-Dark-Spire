// UIGrayscale.shader
// -----------------------------------------------------------------------------
// UI shader implementing Photoshop's "Black & White" adjustment: a per-hue
// channel mixer. Decomposes the sampled sprite into six hue components
// (Reds, Yellows, Greens, Cyans, Blues, Magentas) plus a neutral (achromatic)
// portion, weights each by its corresponding slider, and sums to grayscale.
//
// Matches Photoshop's default preset weights out of the box; slide any one
// up to 3x (300%) or down to 0x for the classic filter-style brightening of
// specific hue families.
//
// Used by SkillInfoPanelUI at runtime and SkillDataEditor for the preview.
// -----------------------------------------------------------------------------
Shader "DarkSpire/UI/BlackAndWhite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Channel weights (0..3 — i.e., 0%..300% in Photoshop UI).
        _WR ("Reds",     Range(0, 3)) = 0.40
        _WY ("Yellows",  Range(0, 3)) = 0.60
        _WG ("Greens",   Range(0, 3)) = 0.40
        _WC ("Cyans",    Range(0, 3)) = 0.60
        _WB ("Blues",    Range(0, 3)) = 0.20
        _WM ("Magentas", Range(0, 3)) = 0.80

        // Standard UI Mask properties — keeps this shader compatible with
        // UGUI RectMask2D / Mask components.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _WR, _WY, _WG, _WC, _WB, _WM;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texCol = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Decompose RGB into 6 hue components + achromatic neutral.
                // This matches Photoshop's Black & White channel mixer logic.
                half r = saturate(texCol.r);
                half g = saturate(texCol.g);
                half b = saturate(texCol.b);

                // Achromatic (white/gray) portion = the min of r,g,b.
                half neutral = min(min(r, g), b);
                half rr = r - neutral;
                half gg = g - neutral;
                half bb = b - neutral;

                // Secondary (CMY) amounts — the min of two primaries after removing neutral.
                half cy = min(gg, bb);
                half mg = min(rr, bb);
                half yl = min(rr, gg);

                // Primary amounts — remaining after secondaries are accounted for.
                half rP = rr - max(yl, mg);
                half gP = gg - max(yl, cy);
                half bP = bb - max(mg, cy);

                half gray = neutral
                          + rP * _WR
                          + yl * _WY
                          + gP * _WG
                          + cy * _WC
                          + bP * _WB
                          + mg * _WM;

                fixed4 col = fixed4(saturate(gray).xxx, texCol.a);

                #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
