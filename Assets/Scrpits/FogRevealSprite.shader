Shader "ARXON/FogRevealSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RevealProgress ("Reveal Progress", Range(0, 1)) = 0
        _RevealSoftness ("Reveal Softness", Range(0.001, 1)) = 0.15
        _FogDistortionStrength ("Fog Distortion Strength", Range(0, 0.05)) = 0.008
        _FogDistortionSpeed ("Fog Distortion Speed", Range(0, 2)) = 0.35
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _RevealProgress;
            float _RevealSoftness;
            float _FogDistortionStrength;
            float _FogDistortionSpeed;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float waveA = sin((uv.y * 6.28318 * 1.7) + (_Time.y * _FogDistortionSpeed));
                float waveB = cos((uv.x * 6.28318 * 1.2) + (_Time.y * (_FogDistortionSpeed * 0.8)));
                uv.x += waveA * _FogDistortionStrength;
                uv.y += waveB * (_FogDistortionStrength * 0.45);

                fixed4 c = tex2D(_MainTex, uv) * IN.color;

                float revealLine = saturate(1.0 - _RevealProgress);
                float softness = max(_RevealSoftness, 0.001);
                float revealedMask = smoothstep(revealLine - softness, revealLine + softness, IN.texcoord.y);
                float alphaMask = 1.0 - revealedMask;

                c.a *= alphaMask;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
