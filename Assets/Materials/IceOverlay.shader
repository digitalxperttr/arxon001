Shader "ARXON/IceOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        
        [Header(Glacial Volumetric Appearance)]
        _IceTint            ("Deep Glacial Ice Tint", Color)              = (0.28, 0.76, 0.98, 1.0)
        _CoreClearTint      ("Core Clear Tint",      Color)              = (0.75, 0.94, 1.0, 1.0)
        _BodyOpacity        ("Center Clear Opacity", Range(0, 1))        = 0.05
        _EdgeOpacity        ("Edge Frost Opacity",   Range(0, 1))        = 0.75
        
        [Header(MultiLayer Crystal Fractures)]
        _CrackColor         ("Crack Glow Color",     Color)              = (0.88, 0.98, 1.0, 1.0)
        _CrackScale         ("Crack Scale (World)",  Range(0.5, 8))      = 2.8
        _CrackThickness     ("Crack Sharpness",      Range(0.01, 0.35))  = 0.075
        _CrackStrength      ("Crack Strength",       Range(0, 3))        = 1.40
        
        [Header(Frame Frostbite and Rime)]
        _RimColor           ("Rim Frost Color",      Color)              = (0.82, 0.96, 1.0, 1.0)
        _RimStrength        ("Rim Strength",         Range(0, 3))        = 1.35
        _RimPower           ("Rim Power Fresnel",    Range(0.5, 6))      = 1.80
        _FrameFrostStrength ("Frame Frost Cover",    Range(0, 1))        = 0.85
        _TopHighlight       ("Top Edge Highlight",   Range(0, 1))        = 0.45

        [Header(Specular Glint and Bubbles)]
        _SpecularColor      ("Specular Diamond Glint", Color)            = (1.0, 1.0, 1.0, 1.0)
        _SpecularStrength   ("Specular Strength",    Range(0, 3))        = 1.20
        _GlintSpeed         ("Glint Sparkle Speed",  Range(0, 5))        = 1.80
        
        [Header(Block Coordinates)]
        _BlockSize          ("Block Size (WH)",      Vector)             = (1, 1, 0, 0)
        _PhaseOffset        ("Phase Offset",         Float)              = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // ── Pass 1: URP 2D Renderer ────────────────────────────────────
        Pass
        {
            Name "IceOverlay_2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IceTint;
                float4 _CoreClearTint;
                float4 _CrackColor;
                float4 _RimColor;
                float4 _SpecularColor;
                float4 _BlockSize;
                float  _BodyOpacity;
                float  _EdgeOpacity;
                float  _CrackScale;
                float  _CrackThickness;
                float  _CrackStrength;
                float  _RimStrength;
                float  _RimPower;
                float  _FrameFrostStrength;
                float  _TopHighlight;
                float  _SpecularStrength;
                float  _GlintSpeed;
                float  _PhaseOffset;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 local01    : TEXCOORD1;
                float2 worldXY    : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 domainWarp(float2 p)
            {
                float n1 = noise2d(p * 1.6);
                float n2 = noise2d(p * 1.6 + float2(5.2, 1.3));
                float2 warp = float2(
                    sin(p.y * 3.8 + n1 * 4.2) * 0.16 + cos(p.x * 2.4) * 0.08,
                    cos(p.x * 3.8 + n2 * 4.2) * 0.16 + sin(p.y * 2.4) * 0.08
                );
                return p + warp;
            }

            float voronoiCracks(float2 uv)
            {
                float2 id = floor(uv);
                float2 st = frac(uv);
                float d1 = 8.0;
                float d2 = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 pointUV = id + neighbor;
                        float2 p = float2(hash21(pointUV), hash21(pointUV + 33.1));
                        float2 pt = neighbor + (0.08 + 0.84 * p);
                        float2 diff = pt - st;
                        float dist = dot(diff, diff);

                        if (dist < d1)
                        {
                            d2 = d1;
                            d1 = dist;
                        }
                        else if (dist < d2)
                        {
                            d2 = dist;
                        }
                    }
                }
                return sqrt(d2) - sqrt(d1);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.color      = IN.color;
                OUT.uv         = IN.uv;

                float2 safeSize = max(_BlockSize.xy, float2(0.01, 0.01));
                OUT.local01 = IN.positionOS.xy / safeSize + 0.5;
                OUT.worldXY = TransformObjectToWorld(IN.positionOS).xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;
                if (spriteAlpha < 0.01) discard;

                float2 p = saturate(IN.local01);

                // ── 1. Fresnel Hacimsel Derinlik & Yuvarlatılmış Taş Köşeleri ─
                float2 localCenter = p - 0.5;
                float cornerR = 0.16; // ARXON taş çerçevesine uyumlu yuvarlatılmış köşe
                float2 q = abs(localCenter) - (float2(0.5, 0.5) - cornerR);
                float sdf = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cornerR;
                float edgeDist = saturate(-sdf / 0.45);

                float fresnel = pow(1.0 - saturate(edgeDist), _RimPower);
                float rim = fresnel * _RimStrength;
                float topRim = pow(p.y, 2.8) * pow(1.0 - saturate(edgeDist), 1.2) * _TopHighlight;

                // Çerçeve donması (Taş çerçevenin üzerini kırağı zırhıyla örter)
                float frameMask = 1.0 - smoothstep(0.06, 0.28, edgeDist);
                float frameFrost = frameMask * _FrameFrostStrength;

                // ── 2. Çok Katmanlı 3D Kristal Çatlaklar (Multi-layer Parallax) ─
                float2 baseUV = IN.worldXY * _CrackScale + _PhaseOffset * float2(0.31, 0.17);
                float2 warpedUV1 = domainWarp(baseUV);

                // Katman 1: Ana Yüzey Çatlakları
                float majorDist = voronoiCracks(warpedUV1);
                float thicknessNoise = noise2d(warpedUV1 * 1.8 + _PhaseOffset);
                float currentThickness = _CrackThickness * (0.60 + 0.85 * thicknessNoise);
                float majorCrack = 1.0 - smoothstep(0.0, currentThickness, majorDist);
                float continuityMask = smoothstep(0.16, 0.46, noise2d(warpedUV1 * 0.95 + 12.4));
                majorCrack = pow(saturate(majorCrack), 1.4) * continuityMask;

                // Katman 2: Derin İç Kılcal Çatlaklar (Parallax Offset)
                float2 deepUV = domainWarp(IN.worldXY * (_CrackScale * 2.1) + _PhaseOffset * float2(-0.25, 0.45) + float2(0.04, -0.04));
                float deepDist = voronoiCracks(deepUV);
                float deepCrack = 1.0 - smoothstep(0.0, _CrackThickness * 0.50, deepDist);
                float deepMask = smoothstep(0.30, 0.70, noise2d(deepUV * 1.2 + 6.3));
                deepCrack = pow(saturate(deepCrack), 2.0) * deepMask * 0.65;

                // Çatlak içi kristal nabzı (Canlı nefes alma)
                float pulse = 0.88 + 0.12 * sin(_Time.y * 2.2 + _PhaseOffset * 2.0);
                float totalCrack = saturate(majorCrack + deepCrack) * _CrackStrength * pulse;

                // Katman 3: Donmuş Mikro Kabarcıklar (Micro-Air Bubbles)
                float bubbleHash = hash21(floor(warpedUV1 * 5.0));
                float bubble = pow(saturate((bubbleHash - 0.93) * 14.0), 3.0) * 0.75;

                // ── 3. Keskin Specular Elmas Parıltıları ───────────────────────
                // Çatlak kesişimlerinde ve yüzey fasetlerinde beyaz elmas ışıltıları
                float glintPhase = _Time.y * _GlintSpeed + _PhaseOffset;
                float glintNoise = sin(warpedUV1.x * 6.0 + warpedUV1.y * 6.0 + glintPhase);
                float specularPoints = pow(saturate(majorCrack * 1.6 + bubble * 2.0), 2.0) * saturate(glintNoise * 1.5 + 0.5);
                float specular = specularPoints * _SpecularStrength;

                // ── 4. Renk ve Katman Kompozisyonu ────────────────────────────
                // Gövde: Derin buzul mavisinden merkezde berrak kristal cama geçiş
                float3 bodyRGB = lerp(_CoreClearTint.rgb, _IceTint.rgb, saturate(fresnel * 1.4 + frameFrost * 0.8));

                // Vurgular: Çatlaklar, kırağı ve specular glint
                float3 highlightRGB = 
                    _CrackColor.rgb    * totalCrack + 
                    _RimColor.rgb      * (rim + topRim + frameFrost) + 
                    _SpecularColor.rgb * specular;

                float highlightWeight = saturate(totalCrack + rim + topRim + frameFrost + specular);
                float3 finalRGB = lerp(bodyRGB, highlightRGB, highlightWeight);

                // Opaklık: Merkezde alt mücevheri gösterecek şeffaflık (_BodyOpacity), 
                // kenarlarda ve çatlaklarda yoğun zırh (_EdgeOpacity)
                float baseAlpha = lerp(_BodyOpacity, _EdgeOpacity, saturate(fresnel + frameFrost));
                float finalAlpha = saturate(baseAlpha + highlightWeight * 0.65 + specular) * spriteAlpha;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }

        // ── Pass 2: URP Forward (Fallback) ────────────────────────────
        Pass
        {
            Name "IceOverlay_Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IceTint;
                float4 _CoreClearTint;
                float4 _CrackColor;
                float4 _RimColor;
                float4 _SpecularColor;
                float4 _BlockSize;
                float  _BodyOpacity;
                float  _EdgeOpacity;
                float  _CrackScale;
                float  _CrackThickness;
                float  _CrackStrength;
                float  _RimStrength;
                float  _RimPower;
                float  _FrameFrostStrength;
                float  _TopHighlight;
                float  _SpecularStrength;
                float  _GlintSpeed;
                float  _PhaseOffset;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 local01    : TEXCOORD1;
                float2 worldXY    : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 domainWarp(float2 p)
            {
                float n1 = noise2d(p * 1.6);
                float n2 = noise2d(p * 1.6 + float2(5.2, 1.3));
                float2 warp = float2(
                    sin(p.y * 3.8 + n1 * 4.2) * 0.16 + cos(p.x * 2.4) * 0.08,
                    cos(p.x * 3.8 + n2 * 4.2) * 0.16 + sin(p.y * 2.4) * 0.08
                );
                return p + warp;
            }

            float voronoiCracks(float2 uv)
            {
                float2 id = floor(uv);
                float2 st = frac(uv);
                float d1 = 8.0;
                float d2 = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 pointUV = id + neighbor;
                        float2 p = float2(hash21(pointUV), hash21(pointUV + 33.1));
                        float2 pt = neighbor + (0.08 + 0.84 * p);
                        float2 diff = pt - st;
                        float dist = dot(diff, diff);

                        if (dist < d1)
                        {
                            d2 = d1;
                            d1 = dist;
                        }
                        else if (dist < d2)
                        {
                            d2 = dist;
                        }
                    }
                }
                return sqrt(d2) - sqrt(d1);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.color      = IN.color;
                OUT.uv         = IN.uv;

                float2 safeSize = max(_BlockSize.xy, float2(0.01, 0.01));
                OUT.local01 = IN.positionOS.xy / safeSize + 0.5;
                OUT.worldXY = TransformObjectToWorld(IN.positionOS).xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;
                if (spriteAlpha < 0.01) discard;

                float2 p = saturate(IN.local01);

                // ── 1. Fresnel Hacimsel Derinlik & Yuvarlatılmış Taş Köşeleri ─
                float2 localCenter = p - 0.5;
                float cornerR = 0.16; // ARXON taş çerçevesine uyumlu yuvarlatılmış köşe
                float2 q = abs(localCenter) - (float2(0.5, 0.5) - cornerR);
                float sdf = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cornerR;
                float edgeDist = saturate(-sdf / 0.45);

                float fresnel = pow(1.0 - saturate(edgeDist), _RimPower);
                float rim = fresnel * _RimStrength;
                float topRim = pow(p.y, 2.8) * pow(1.0 - saturate(edgeDist), 1.2) * _TopHighlight;

                float frameMask = 1.0 - smoothstep(0.06, 0.28, edgeDist);
                float frameFrost = frameMask * _FrameFrostStrength;

                float2 baseUV = IN.worldXY * _CrackScale + _PhaseOffset * float2(0.31, 0.17);
                float2 warpedUV1 = domainWarp(baseUV);

                float majorDist = voronoiCracks(warpedUV1);
                float thicknessNoise = noise2d(warpedUV1 * 1.8 + _PhaseOffset);
                float currentThickness = _CrackThickness * (0.60 + 0.85 * thicknessNoise);
                float majorCrack = 1.0 - smoothstep(0.0, currentThickness, majorDist);
                float continuityMask = smoothstep(0.16, 0.46, noise2d(warpedUV1 * 0.95 + 12.4));
                majorCrack = pow(saturate(majorCrack), 1.4) * continuityMask;

                float2 deepUV = domainWarp(IN.worldXY * (_CrackScale * 2.1) + _PhaseOffset * float2(-0.25, 0.45) + float2(0.04, -0.04));
                float deepDist = voronoiCracks(deepUV);
                float deepCrack = 1.0 - smoothstep(0.0, _CrackThickness * 0.50, deepDist);
                float deepMask = smoothstep(0.30, 0.70, noise2d(deepUV * 1.2 + 6.3));
                deepCrack = pow(saturate(deepCrack), 2.0) * deepMask * 0.65;

                float pulse = 0.88 + 0.12 * sin(_Time.y * 2.2 + _PhaseOffset * 2.0);
                float totalCrack = saturate(majorCrack + deepCrack) * _CrackStrength * pulse;

                float bubbleHash = hash21(floor(warpedUV1 * 5.0));
                float bubble = pow(saturate((bubbleHash - 0.93) * 14.0), 3.0) * 0.75;

                float glintPhase = _Time.y * _GlintSpeed + _PhaseOffset;
                float glintNoise = sin(warpedUV1.x * 6.0 + warpedUV1.y * 6.0 + glintPhase);
                float specularPoints = pow(saturate(majorCrack * 1.6 + bubble * 2.0), 2.0) * saturate(glintNoise * 1.5 + 0.5);
                float specular = specularPoints * _SpecularStrength;

                float3 bodyRGB = lerp(_CoreClearTint.rgb, _IceTint.rgb, saturate(fresnel * 1.4 + frameFrost * 0.8));

                float3 highlightRGB = 
                    _CrackColor.rgb    * totalCrack + 
                    _RimColor.rgb      * (rim + topRim + frameFrost) + 
                    _SpecularColor.rgb * specular;

                float highlightWeight = saturate(totalCrack + rim + topRim + frameFrost + specular);
                float3 finalRGB = lerp(bodyRGB, highlightRGB, highlightWeight);

                float baseAlpha = lerp(_BodyOpacity, _EdgeOpacity, saturate(fresnel + frameFrost));
                float finalAlpha = saturate(baseAlpha + highlightWeight * 0.65 + specular) * spriteAlpha;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
