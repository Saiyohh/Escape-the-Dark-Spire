// SpriteGrayscale.shader
// -----------------------------------------------------------------------------
// SpriteRenderer-friendly variant of UIGrayscale — same per-hue Black & White
// channel mixer, but without the UI-specific Stencil block / unity_GUIZTestMode
// reference that breaks rendering when applied to a world-space SpriteRenderer.
//
// Use this shader for the material assigned to UnitDisplay.blackAndWhiteMaterial.
// The UI variant (DarkSpire/UI/BlackAndWhite) is for UGUI Image components.
// -----------------------------------------------------------------------------
Shader "DarkSpire/Sprite/BlackAndWhite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Channel weights (0..3 — 0%..300% in Photoshop UI).
        _WR ("Reds",     Range(0, 3)) = 0.40
        _WY ("Yellows",  Range(0, 3)) = 0.60
        _WG ("Greens",   Range(0, 3)) = 0.40
        _WC ("Cyans",    Range(0, 3)) = 0.60
        _WB ("Blues",    Range(0, 3)) = 0.20
        _WM ("Magentas", Range(0, 3)) = 0.80

        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _RendererColor ("RendererColor", Color) = (1,1,1,1)
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _WR, _WY, _WG, _WC, _WB, _WM;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Decompose RGB into 6 hue components + achromatic neutral.
                // Matches Photoshop's Black & White channel mixer logic.
                half r = saturate(texCol.r);
                half g = saturate(texCol.g);
                half b = saturate(texCol.b);

                half neutral = min(min(r, g), b);
                half rr = r - neutral;
                half gg = g - neutral;
                half bb = b - neutral;

                half cy = min(gg, bb);
                half mg = min(rr, bb);
                half yl = min(rr, gg);

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

                return fixed4(saturate(gray).xxx, texCol.a);
            }
            ENDCG
        }
    }
}
