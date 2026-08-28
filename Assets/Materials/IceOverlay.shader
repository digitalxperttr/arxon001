Shader "ARXON/IceOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        
        [Header(Base Ice Appearance)]
        _IceTint            ("Ice Tint",             Color)              = (0.72, 0.92, 1.0, 1.0)
        _BodyOpacity        ("Body Frost Opacity",   Range(0, 1))        = 0.20
        
        [Header(Chaotic Crystal Fractures)]
        _CrackColor         ("Crack Color",          Color)              = (0.92, 0.98, 1.0, 1.0)
        _CrackScale         ("Crack Scale (World)",  Range(0.5, 8))      = 2.6
        _CrackThickness     ("Crack Sharpness",      Range(0.01, 0.35))  = 0.08
        _CrackStrength      ("Crack Strength",       Range(0, 2))        = 0.90
        
        [Header(Frozen Rim and Frame Frost)]
        _RimColor           ("Rim Color",            Color)              = (0.88, 0.96, 1.0, 1.0)
        _RimStrength        ("Rim Strength",         Range(0, 2))        = 0.85
        _RimPower           ("Rim Power",            Range(0.5, 6))      = 2.8
        _FrameFrostStrength ("Frame Frost Cover",    Range(0, 1))        = 0.85
        _TopHighlight       ("Top Edge Highlight",   Range(0, 1))        = 0.30

        [Header(Specular Sheen Sweep)]
        _SheenColor         ("Sheen Color",          Color)              = (1.0, 1.0, 1.0, 1.0)
        _SheenStrength      ("Sheen Strength",       Range(0, 2))        = 0.60
        _SheenSpeed         ("Sheen Speed",          Range(0, 3))        = 0.75
        _SheenWidth         ("Sheen Width",          Range(1, 20))       = 7.0
        
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
                float4 _CrackColor;
                float4 _RimColor;
                float4 _SheenColor;
                float4 _BlockSize;
                float  _BodyOpacity;
                float  _CrackScale;
                float  _CrackThickness;
                float  _CrackStrength;
                float  _RimStrength;
                float  _RimPower;
                float  _FrameFrostStrength;
                float  _TopHighlight;
                float  _SheenStrength;
                float  _SheenSpeed;
                float  _SheenWidth;
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
                float2 local01    : TEXCOORD1;  // [0,1] normalize blok koordinatı
                float2 worldXY    : TEXCOORD2;  // world-space koordinat
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

            // Domain warping: Petek görüntüsünü kırıp kaotik, zikzaklı çatlak hatları oluşturur
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

            // Voronoi F2 - F1: Asimetrik jitter ile rastgele kristal damarları
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

                // 1. Kaotik ve Değişken Kalınlıkta Buz Damarları (Multi-scale Chaotic Fractures)
                float2 baseUV = IN.worldXY * _CrackScale + _PhaseOffset * float2(0.31, 0.17);
                float2 warpedUV1 = domainWarp(baseUV);

                // Ana damarlar (Major Veins) + kalınlık modülasyonu
                float majorDist = voronoiCracks(warpedUV1);
                float thicknessNoise = noise2d(warpedUV1 * 1.8 + _PhaseOffset);
                float currentThickness = _CrackThickness * (0.55 + 0.9 * thicknessNoise);
                float majorCrack = 1.0 - smoothstep(0.0, currentThickness, majorDist);
                // Doku süreklilik maskesi: bazı petek kenarlarını kesip organik dallanma yapar
                float continuityMask = smoothstep(0.18, 0.48, noise2d(warpedUV1 * 0.95 + 12.4));
                majorCrack = pow(saturate(majorCrack), 1.5) * continuityMask;

                // İnce kılcal çatlaklar (Micro Veins)
                float2 warpedUV2 = domainWarp(IN.worldXY * (_CrackScale * 2.3) + _PhaseOffset * float2(-0.23, 0.41));
                float microDist = voronoiCracks(warpedUV2);
                float microCrack = 1.0 - smoothstep(0.0, _CrackThickness * 0.45, microDist);
                float microMask = smoothstep(0.32, 0.68, noise2d(warpedUV2 * 1.25 + 8.1));
                microCrack = pow(saturate(microCrack), 2.0) * microMask * 0.55;

                float crack = saturate(majorCrack + microCrack) * _CrackStrength;

                // 2. Çerçeve Donması (Frame Frost) - Alttaki sarı altın kenarı buz zırhı ile kaplar
                float edgeX = min(p.x, 1.0 - p.x) * 2.0;
                float edgeY = min(p.y, 1.0 - p.y) * 2.0;
                float edgeDist = min(edgeX, edgeY);

                // Kenar donma maskesi: dış sınırda (altın çerçevenin olduğu bölgede) buz katmanını yoğunlaştırır
                float frameMask = 1.0 - smoothstep(0.08, 0.32, edgeDist);
                float frameFrost = frameMask * _FrameFrostStrength;

                // Rim ve Üst Işık
                float rim = pow(1.0 - saturate(edgeDist), _RimPower) * _RimStrength;
                float topRim = pow(p.y, 2.5) * pow(1.0 - saturate(edgeDist), 1.2) * _TopHighlight;

                // 3. Periyodik Işık Yansıması (Specular Sheen Sweep)
                float sweepProgress = frac(_Time.y * _SheenSpeed * 0.2 + _PhaseOffset * 0.08);
                float diag = p.x * 0.65 + p.y * 0.35;
                float distFromSweep = abs(diag - (sweepProgress * 1.8 - 0.4));
                float sheen = saturate(1.0 - distFromSweep * _SheenWidth);
                sheen = pow(sheen, 3.5) * _SheenStrength;

                // 4. Renk ve Katman Kompozisyonu
                float facet = hash21(floor(baseUV));
                float3 bodyRGB = _IceTint.rgb * (0.92 + 0.16 * facet);

                float highlightSum = crack + rim + topRim + sheen + frameFrost;
                float safeHighlightSum = max(highlightSum, 0.0001);

                float3 highlightRGB = 
                    (_CrackColor.rgb * crack + 
                     _RimColor.rgb   * (rim + topRim + frameFrost) + 
                     _SheenColor.rgb * sheen) / safeHighlightSum;

                float highlightWeight = saturate(highlightSum);
                float3 finalRGB = lerp(bodyRGB, highlightRGB, highlightWeight);

                // Gövde merkezinde taşın canlı rengi okunur; dış çerçeve ve çatlaklarda buz zırhı opaktır
                float baseBody = _BodyOpacity * (1.0 - highlightWeight * 0.4);
                float finalAlpha = saturate(baseBody + highlightWeight) * spriteAlpha;

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
                float4 _CrackColor;
                float4 _RimColor;
                float4 _SheenColor;
                float4 _BlockSize;
                float  _BodyOpacity;
                float  _CrackScale;
                float  _CrackThickness;
                float  _CrackStrength;
                float  _RimStrength;
                float  _RimPower;
                float  _FrameFrostStrength;
                float  _TopHighlight;
                float  _SheenStrength;
                float  _SheenSpeed;
                float  _SheenWidth;
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

                // 1. Kaotik ve Değişken Kalınlıkta Buz Damarları
                float2 baseUV = IN.worldXY * _CrackScale + _PhaseOffset * float2(0.31, 0.17);
                float2 warpedUV1 = domainWarp(baseUV);

                float majorDist = voronoiCracks(warpedUV1);
                float thicknessNoise = noise2d(warpedUV1 * 1.8 + _PhaseOffset);
                float currentThickness = _CrackThickness * (0.55 + 0.9 * thicknessNoise);
                float majorCrack = 1.0 - smoothstep(0.0, currentThickness, majorDist);
                float continuityMask = smoothstep(0.18, 0.48, noise2d(warpedUV1 * 0.95 + 12.4));
                majorCrack = pow(saturate(majorCrack), 1.5) * continuityMask;

                float2 warpedUV2 = domainWarp(IN.worldXY * (_CrackScale * 2.3) + _PhaseOffset * float2(-0.23, 0.41));
                float microDist = voronoiCracks(warpedUV2);
                float microCrack = 1.0 - smoothstep(0.0, _CrackThickness * 0.45, microDist);
                float microMask = smoothstep(0.32, 0.68, noise2d(warpedUV2 * 1.25 + 8.1));
                microCrack = pow(saturate(microCrack), 2.0) * microMask * 0.55;

                float crack = saturate(majorCrack + microCrack) * _CrackStrength;

                // 2. Çerçeve Donması
                float edgeX = min(p.x, 1.0 - p.x) * 2.0;
                float edgeY = min(p.y, 1.0 - p.y) * 2.0;
                float edgeDist = min(edgeX, edgeY);

                float frameMask = 1.0 - smoothstep(0.08, 0.32, edgeDist);
                float frameFrost = frameMask * _FrameFrostStrength;

                float rim = pow(1.0 - saturate(edgeDist), _RimPower) * _RimStrength;
                float topRim = pow(p.y, 2.5) * pow(1.0 - saturate(edgeDist), 1.2) * _TopHighlight;

                // 3. Periyodik Işık Parlaması
                float sweepProgress = frac(_Time.y * _SheenSpeed * 0.2 + _PhaseOffset * 0.08);
                float diag = p.x * 0.65 + p.y * 0.35;
                float distFromSweep = abs(diag - (sweepProgress * 1.8 - 0.4));
                float sheen = saturate(1.0 - distFromSweep * _SheenWidth);
                sheen = pow(sheen, 3.5) * _SheenStrength;

                // 4. Renk Kompozisyonu
                float facet = hash21(floor(baseUV));
                float3 bodyRGB = _IceTint.rgb * (0.92 + 0.16 * facet);

                float highlightSum = crack + rim + topRim + sheen + frameFrost;
                float safeHighlightSum = max(highlightSum, 0.0001);

                float3 highlightRGB = 
                    (_CrackColor.rgb * crack + 
                     _RimColor.rgb   * (rim + topRim + frameFrost) + 
                     _SheenColor.rgb * sheen) / safeHighlightSum;

                float highlightWeight = saturate(highlightSum);
                float3 finalRGB = lerp(bodyRGB, highlightRGB, highlightWeight);

                float baseBody = _BodyOpacity * (1.0 - highlightWeight * 0.4);
                float finalAlpha = saturate(baseBody + highlightWeight) * spriteAlpha;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}
