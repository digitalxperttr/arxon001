Shader "ARXON/MysticForge/ForgeEnergyLeaks"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _LeakTime ("Leak Time", Float) = 0
        _LeakIntensity ("Leak Intensity", Range(0, 2)) = 1
        _HighlightSpeedA ("Highlight Speed A", Float) = 0.16
        _HighlightSpeedB ("Highlight Speed B", Float) = 0.095
        _HighlightWidthA ("Highlight Width A", Range(0.01, 0.3)) = 0.085
        _HighlightWidthB ("Highlight Width B", Range(0.01, 0.3)) = 0.055
        _HighlightIntensityA ("Highlight Intensity A", Range(0, 1)) = 0.42
        _HighlightIntensityB ("Highlight Intensity B", Range(0, 1)) = 0.26
        _AlphaMin ("Alpha Min", Range(0, 1)) = 0.72
        _AlphaMax ("Alpha Max", Range(0, 1)) = 1
        _AlphaPulseSpeed ("Alpha Pulse Speed", Float) = 0.13
        _PhaseSpread ("Phase Spread", Float) = 18
        _EdgeFade ("Edge Fade", Range(0, 0.2)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForgeEnergyLeaks"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _LeakTime;
                float _LeakIntensity;
                float _HighlightSpeedA;
                float _HighlightSpeedB;
                float _HighlightWidthA;
                float _HighlightWidthB;
                float _HighlightIntensityA;
                float _HighlightIntensityB;
                float _AlphaMin;
                float _AlphaMax;
                float _AlphaPulseSpeed;
                float _PhaseSpread;
                float _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float twoPi = 6.28318530718;
                float phase = input.uv.x * _PhaseSpread;
                float lanePhase = frac(sin(floor(input.uv.x * 19.0) * 12.9898) * 43758.5453) * twoPi;

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float headA = frac(-_LeakTime * _HighlightSpeedA + phase * 0.031 + lanePhase * 0.071);
                float headB = frac(-_LeakTime * _HighlightSpeedB + phase * 0.047 + lanePhase * 0.113 + 0.43);
                float deltaA = abs(frac(input.uv.y - headA + 0.5) - 0.5);
                float deltaB = abs(frac(input.uv.y - headB + 0.5) - 0.5);
                float bandA = smoothstep(_HighlightWidthA, 0.0, deltaA);
                float bandB = smoothstep(_HighlightWidthB, 0.0, deltaB);
                float highlight = bandA * _HighlightIntensityA + bandB * _HighlightIntensityB;

                float pulse = 0.5 + 0.5 * sin(_LeakTime * _AlphaPulseSpeed * twoPi + phase + lanePhase);
                float alphaPulse = lerp(_AlphaMin, _AlphaMax, pulse);

                float edge = max(_EdgeFade, 0.0001);
                float edgeMask =
                    smoothstep(0.0, edge, input.uv.x) *
                    smoothstep(0.0, edge, input.uv.y) *
                    smoothstep(0.0, edge, 1.0 - input.uv.x) *
                    smoothstep(0.0, edge, 1.0 - input.uv.y);

                texel.rgb *= 1.0 + highlight;
                texel.a *= saturate(alphaPulse + highlight * 0.18) * _LeakIntensity * edgeMask;
                return texel * input.color;
            }
            ENDHLSL
        }
    }
}
