Shader "ARXON/Fire V2/Symbol Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 0.92, 0.65, 0.65)
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.35
        _GlowRadius ("Glow Radius", Range(0.5, 8)) = 2.5
        _GlowSoftness ("Glow Softness", Range(0.25, 3)) = 1.25
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "FireSymbolGlow"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _RendererColor;
                float _GlowIntensity;
                float _GlowRadius;
                float _GlowSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * _RendererColor;
                output.uv = input.uv;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _GlowRadius;
                float2 diagonal = texel * 0.70710678;

                half center = SampleAlpha(input.uv) * 0.20h;
                half innerRing =
                    SampleAlpha(input.uv + float2(texel.x, 0.0)) +
                    SampleAlpha(input.uv - float2(texel.x, 0.0)) +
                    SampleAlpha(input.uv + float2(0.0, texel.y)) +
                    SampleAlpha(input.uv - float2(0.0, texel.y));
                half diagonalRing =
                    SampleAlpha(input.uv + float2(diagonal.x, diagonal.y)) +
                    SampleAlpha(input.uv + float2(-diagonal.x, diagonal.y)) +
                    SampleAlpha(input.uv + float2(diagonal.x, -diagonal.y)) +
                    SampleAlpha(input.uv - float2(diagonal.x, diagonal.y));

                half blurredAlpha = center + (innerRing * 0.11h) + (diagonalRing * 0.09h);
                blurredAlpha = saturate(blurredAlpha);
                half softenedAlpha = pow(blurredAlpha, max(0.25h, (half)_GlowSoftness));
                half alpha = saturate(softenedAlpha * _GlowColor.a * input.color.a * _GlowIntensity);

                return half4(_GlowColor.rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
