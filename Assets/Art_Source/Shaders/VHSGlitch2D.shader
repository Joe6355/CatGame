Shader "Custom/VHSGlitch2D_Hardcore"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        _GlitchStrength ("Glitch Strength", Range(0,1)) = 0.1
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.2
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.3
        _TimeScale ("Time Scale", Float) = 1.0

        _FlipStrength ("Flip Strength", Range(0,1)) = 0.2
        _TearStrength ("Tear Strength", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float _GlitchStrength;
            float _NoiseStrength;
            float _ScanlineStrength;
            float _TimeScale;

            float _FlipStrength;
            float _TearStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float time = _Time.y * _TimeScale;

                float2 uv = i.uv;

                fixed4 original = tex2D(_MainTex, uv);

                if (original.a < 0.1)
                    discard;

                // =====================================
                // 🔥 СИЛЬНЫЙ VHS РАЗРЫВ (КРУПНЫЕ БЛОКИ)
                // =====================================

                // крупные горизонтальные блоки
                float block = floor(uv.y * 10); // чем меньше число — тем больше куски
                float blockRand = rand(float2(block, floor(time * 3)));

                // сильный горизонтальный сдвиг блока
                if (blockRand > 0.85)
                {
                    float shift = (rand(float2(block, time)) - 0.5) * _TearStrength * 3.0;
                    uv.x += shift;
                }

                // =====================================
                // 💥 РЕЗКИЕ РАЗРЫВЫ (как в VHS)
                // =====================================

                float tearLine = step(0.95, rand(float2(time * 2, uv.y)));

                if (tearLine > 0.0)
                {
                    uv.x += (rand(float2(uv.y, time)) - 0.5) * _TearStrength * 5.0;
                    uv.y += (rand(float2(time, uv.x)) - 0.5) * _FlipStrength * 0.5;
                }

                // =====================================
                // ⚡ ВЕРТИКАЛЬНЫЙ СЛОМ (frame jump)
                // =====================================

                float jump = step(0.98, rand(float2(time, 0.0)));

                if (jump > 0.5)
                {
                    uv.y += (rand(float2(time, 1.0)) - 0.5) * _FlipStrength * 2.0;
                }

                // =====================================
                // 📼 ДРОЖАНИЕ ЛЕНТЫ (сильнее)
                // =====================================

                uv.x += sin(uv.y * 80 + time * 15) * _TearStrength;
                uv.y += sin(uv.x * 40 + time * 10) * _TearStrength * 0.3;

                // clamp чтобы не ломалось
                uv = clamp(uv, 0.0, 1.0);

                // =====================================
                // ГЛИЧ СТРОКИ
                // =====================================

                float glitchLine = step(0.9, rand(float2(time, uv.y)));
                uv.x += glitchLine * (_GlitchStrength * (rand(float2(uv.y, time)) - 0.5) * 2.0);

                // =====================================
                // RGB SPLIT (усилен)
                // =====================================

                float offset = _GlitchStrength * 0.02;

                float r = tex2D(_MainTex, uv + float2(offset,0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(offset,0)).b;

                fixed4 col = fixed4(r, g, b, original.a);

                // =====================================
                // ШУМ (усилен)
                // =====================================

                float noise = rand(uv * time * 2.0) * _NoiseStrength * 1.5;
                col.rgb += noise;

                // =====================================
                // СКАНЛАЙНЫ
                // =====================================

                float scan = sin(uv.y * 1000) * _ScanlineStrength;
                col.rgb -= scan;

                return col;
            }
            ENDCG
        }
    }
}