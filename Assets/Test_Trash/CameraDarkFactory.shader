Shader "Hidden/Custom/FullscreenThinDistortionScanLine"
{
    Properties
    {
        [Header(Scan Line)]
        _LineOpacity ("Line Opacity", Range(0, 1)) = 0.85
        _LineThickness ("Line Thickness", Range(0.001, 0.05)) = 0.01
        _LineSoftness ("Line Softness", Range(0.0001, 0.02)) = 0.002
        _LineSpeed ("Line Speed", Range(0, 5)) = 0.45

        [Header(Distortion)]
        _DistortionX ("Distortion X", Range(0, 0.05)) = 0.01
        _JitterStrength ("Jitter Strength", Range(0, 0.02)) = 0.002
        _JitterScale ("Jitter Scale", Range(1, 200)) = 80

        [Header(Optional Tint)]
        _LineTintStrength ("Line Tint Strength", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "ThinDistortionScanLine"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _LineOpacity;
            float _LineThickness;
            float _LineSoftness;
            float _LineSpeed;

            float _DistortionX;
            float _JitterStrength;
            float _JitterScale;

            float _LineTintStrength;

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;

                // Исходная картинка
                half4 originalColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Положение полоски сверху вниз
                float lineY = 1.0 - frac(_Time.y * _LineSpeed);

                // Ровная тонкая горизонтальная полоска
                float dist = abs(uv.y - lineY);

                float lineMask = 1.0 - smoothstep(
                    _LineThickness,
                    _LineThickness + _LineSoftness,
                    dist
                );

                lineMask *= _LineOpacity;

                // Искажение только внутри полоски
                // Основной сдвиг всей картинки под полоской по X
                float shiftX = sin(_Time.y * 40.0) * _DistortionX;

                // Мелкий цифровой jitter, но НЕ меняющий форму полоски
                float jitter = (Hash(floor(uv.x * _JitterScale) + floor(_Time.y * 60.0)) - 0.5) * 2.0;
                shiftX += jitter * _JitterStrength;

                float2 distortedUV = uv;
                distortedUV.x += shiftX * lineMask;
                distortedUV = saturate(distortedUV);

                half4 distortedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV);

                // Очень лёгкий визуальный оттенок у полоски, чтобы её было видно
                distortedColor.rgb += lineMask * _LineTintStrength;

                // Прозрачное смешивание:
                // вне полоски = оригинал
                // в полоске = искажённая картинка
                half4 finalColor = lerp(originalColor, distortedColor, lineMask);

                return finalColor;
            }

            ENDHLSL
        }
    }
}