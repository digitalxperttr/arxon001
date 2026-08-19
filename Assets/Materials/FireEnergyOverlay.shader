Shader "ARXON/FireEnergyOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _EnergyColor ("Energy Color", Color) = (1, 0.36, 0.04, 0.42)
        _BlockSize ("Block Size", Vector) = (1, 1, 0, 0)
        _PhaseOffset ("Phase Offset", Float) = 0
        _Intensity ("Intensity", Range(0, 1)) = 0.46
        _Density ("Density", Range(0.5, 8)) = 3.2
        _Softness ("Softness", Range(0.05, 1)) = 0.46
        _FlowSpeed ("Flow Speed", Range(0.05, 2)) = 0.62
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FireEnergyOverlay"
            Tags { "LightMode"="Universal2D" }

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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 local01 : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _EnergyColor;
                float4 _BlockSize;
                float _PhaseOffset;
                float _Intensity;
                float _Density;
                float _Softness;
                float _FlowSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.color = IN.color;
                OUT.uv = IN.uv;

                float2 safeSize = max(_BlockSize.xy, float2(0.01, 0.01));
                OUT.local01 = IN.positionOS.xy / safeSize + 0.5;
                return OUT;
            }

            float Ribbon(float2 p, float index, float ribbonCount, float t)
            {
                float enabled = step(index + 0.5, ribbonCount);
                float lane = (index + 0.5) / max(1.0, ribbonCount);
                float irregular = sin((index + 1.0) * 12.989 + _PhaseOffset) * 0.055;
                float baseX = saturate(lane + irregular);

                float phase = _PhaseOffset + index * 1.73;
                float speed = 0.82 + frac(sin(index * 23.17 + 2.0) * 13.51) * 0.34;
                float swayA = sin(p.y * (4.6 + index * 0.37) + t * (0.95 + index * 0.04) + phase) * 0.035;
                float swayB = sin(p.y * (8.2 + index * 0.29) - t * 0.55 + phase * 1.3) * 0.018;
                float centerX = baseX + swayA + swayB;

                float ribbonWidth = lerp(0.050, 0.034, saturate((ribbonCount - 3.0) / 3.0));
                ribbonWidth *= lerp(1.08, 0.82, saturate((_Density - 0.5) / 7.5));

                float distanceToRibbon = abs(p.x - centerX);
                float body = exp(-distanceToRibbon * distanceToRibbon / max(0.0001, ribbonWidth * ribbonWidth));

                float flow = frac(p.y * (1.15 + index * 0.11) - t * speed + phase);
                float segment = smoothstep(0.06, 0.30, flow) * (1.0 - smoothstep(0.66, 0.96, flow));
                float breathe = 0.72 + 0.28 * sin(t * (1.2 + index * 0.13) + phase * 2.1);

                return body * segment * breathe * enabled;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;
                float2 p = saturate(IN.local01);

                float edge = min(min(p.x, 1.0 - p.x), min(p.y, 1.0 - p.y));
                float edgeMask = smoothstep(0.0, max(0.001, _Softness * 0.28), edge);

                float t = _Time.y * _FlowSpeed + _PhaseOffset;
                float widthScale = max(1.0, _BlockSize.x);
                float visualWidth = clamp(floor(widthScale + 0.5), 1.0, 4.0);
                float ribbonCount = visualWidth + 2.0;

                float energy = 0.0;
                energy += Ribbon(p, 0.0, ribbonCount, t);
                energy += Ribbon(p, 1.0, ribbonCount, t);
                energy += Ribbon(p, 2.0, ribbonCount, t);
                energy += Ribbon(p, 3.0, ribbonCount, t);
                energy += Ribbon(p, 4.0, ribbonCount, t);
                energy += Ribbon(p, 5.0, ribbonCount, t);
                energy = saturate(energy * 0.82);

                float alpha = energy * edgeMask * spriteAlpha * _Intensity * _EnergyColor.a;
                return half4(_EnergyColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
