// Additive UI shader with gold gradient, UV scroll, and edge feathering.
// Black becomes transparent, bright parts glow additively.
// Use for particle sprites, light beams, sparkles, etc. on UI Canvas.
Shader "UI/Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Gold gradient: white-hot center → colored edges (across beam thickness / U axis)
        _GradientColor ("Gradient Edge Color", Color) = (1, 0.75, 0.2, 1)
        _GradientStrength ("Gradient Strength", Range(0, 1)) = 0
        _GradientPower ("Gradient Falloff", Range(0.5, 5)) = 1.5

        // UV scroll along beam length (V axis) for flowing energy feel
        _ScrollSpeed ("UV Scroll Speed", Float) = 0

        // Edge feathering: soft fade at beam ends (along V axis)
        _EdgeFade ("Edge Fade Width", Range(0, 0.5)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
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
        // ADDITIVE BLENDING: output = src * srcAlpha + dst * 1
        Blend SrcAlpha One
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

            fixed4 _GradientColor;
            float _GradientStrength;
            float _GradientPower;
            float _ScrollSpeed;
            float _EdgeFade;

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
                float2 uv = IN.texcoord;

                // UV scroll along beam length (V axis) for flowing energy
                float2 scrolledUV = float2(uv.x, frac(uv.y + _Time.y * _ScrollSpeed));

                // Sample texture
                half4 texColor = tex2D(_MainTex, scrolledUV) + _TextureSampleAdd;

                // Gold gradient: white-hot center → gradient color at edges (across U = thickness)
                float centerDist = abs(uv.x - 0.5) * 2.0; // 0 at center, 1 at edges
                float gradientMix = pow(centerDist, _GradientPower);
                float3 goldGradient = lerp(float3(1, 1, 1), _GradientColor.rgb, gradientMix);
                // Blend between no gradient (1,1,1) and full gradient based on strength
                float3 finalGradient = lerp(float3(1, 1, 1), goldGradient, _GradientStrength);

                // Edge feathering: soft fade at beam ends (along V = length)
                float edgeMask = 1.0;
                if (_EdgeFade > 0.002)
                {
                    edgeMask = smoothstep(0.0, _EdgeFade, uv.y) * smoothstep(1.0, 1.0 - _EdgeFade, uv.y);
                }

                // Combine: texture * gradient * vertex color, with edge fade on alpha
                half4 color;
                color.rgb = texColor.rgb * finalGradient * IN.color.rgb;
                color.a = texColor.a * IN.color.a * edgeMask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // Premultiply alpha for proper additive fade-out
                color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }
}
