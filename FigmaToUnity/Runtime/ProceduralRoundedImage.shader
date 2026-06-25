Shader "FigmaToUnity/UI/Procedural Rounded Image"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localRectData : TEXCOORD1;
                float4 cornerRadii : TEXCOORD2;
                float4 strokeColor : TEXCOORD3;
                float2 strokeData : TEXCOORD4;
                float4 worldPosition : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // Signed distance to a rounded rectangle with per-corner radii.
            // Returns negative inside, positive outside (standard SDF convention).
            // pixelPos is in [0, size] coordinate space.
            float RoundedRectSdf(float2 pixelPos, float2 size, float4 radii)
            {
                // Pick the correct radius for this quadrant:
                //   radii = (topLeft, topRight, bottomRight, bottomLeft)
                float2 halfSize = size * 0.5;
                float2 centered = pixelPos - halfSize;

                // Select radius: x=topLeft, y=topRight, z=bottomRight, w=bottomLeft
                float r = (centered.x < 0.0)
                    ? ((centered.y < 0.0) ? radii.x : radii.w)
                    : ((centered.y < 0.0) ? radii.y : radii.z);

                float2 q = abs(centered) - halfSize + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            float SampleFillAlpha(float sdf, float falloff)
            {
                return saturate(-sdf / max(falloff, 0.0001));
            }

            float SampleInnerFillLerp(float sdf, float strokeWidth, float falloff)
            {
                if (strokeWidth <= 0.0001)
                {
                    return 1.0;
                }

                return saturate(-(sdf + strokeWidth) / max(falloff, 0.0001));
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.color = v.color * _Color;
                OUT.texcoord = v.texcoord;
                OUT.localRectData = v.uv1;
                OUT.cornerRadii = v.uv2;
                OUT.strokeColor = v.tangent;
                OUT.strokeData = float2(v.normal.x, v.normal.y);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float2 normalizedPos = IN.localRectData.xy;
                float2 rectSize = IN.localRectData.zw;
                float2 pixelPos = normalizedPos * rectSize;
                float4 radii = IN.cornerRadii;
                float strokeWidth = IN.strokeData.x;
                float falloff = max(IN.strokeData.y, 0.01);

                float sdf = RoundedRectSdf(pixelPos, rectSize, radii);
                float fillAlpha = SampleFillAlpha(sdf, falloff);
                float innerFillLerp = SampleInnerFillLerp(sdf, strokeWidth, falloff);

                fixed4 strokeColor = IN.strokeColor;
                fixed4 finalColor = lerp(strokeColor, baseColor, innerFillLerp);
                finalColor.a *= fillAlpha;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
