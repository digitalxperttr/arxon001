Shader "ARXON/MysticForge/ForgeEnergyWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WaveTime ("Wave Time", Float) = 0
        _WaveAmplitudeA ("Wave Amplitude A", Range(0, 0.08)) = 0.042
        _WaveFrequencyA ("Wave Frequency A", Float) = 2.1
        _WaveSpeedA ("Wave Speed A", Float) = 0.23
        _WaveAmplitudeB ("Wave Amplitude B", Range(0, 0.08)) = 0.018
        _WaveFrequencyB ("Wave Frequency B", Float) = 4.3
        _WaveSpeedB ("Wave Speed B", Float) = 0.31
        _VerticalBias ("Vertical Bias", Range(-0.05, 0.05)) = 0
        _WaveIntensity ("Wave Intensity", Range(0, 2)) = 1
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
            Name "ForgeEnergyWave"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WaveTime;
                float _WaveAmplitudeA;
                float _WaveFrequencyA;
                float _WaveSpeedA;
                float _WaveAmplitudeB;
                float _WaveFrequencyB;
                float _WaveSpeedB;
                float _VerticalBias;
                float _WaveIntensity;
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
                float phaseA = input.uv.x * _WaveFrequencyA * twoPi + _WaveTime * _WaveSpeedA * twoPi;
                float phaseB = input.uv.x * _WaveFrequencyB * twoPi - _WaveTime * _WaveSpeedB * twoPi;
                float wave = (sin(phaseA) * _WaveAmplitudeA + sin(phaseB) * _WaveAmplitudeB + _VerticalBias) * _WaveIntensity;
                float2 distortedUv = input.uv + float2(0, wave);

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUv);
                return texel * input.color;
            }
            ENDHLSL
        }
    }
}
