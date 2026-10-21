Shader "Hidden/PixelateDither"
{
    Properties
    {
        _PixelSize ("Pixel Size", Float) = 4.0
        _ColorLevels ("Color Levels", Float) = 8.0
        _DitherStrength ("Dither Strength", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "PixelateDither"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _PixelSize;
            float _ColorLevels;
            float _DitherStrength;

            // Матрица Байера 4x4
            static const float DITHER_MATRIX[16] = {
                0.0, 8.0, 2.0, 10.0,
                12.0, 4.0, 14.0, 6.0,
                3.0, 11.0, 1.0, 9.0,
                15.0, 7.0, 13.0, 5.0
            };

            float4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // 1. Пикселизация UV
                float2 screenSize = _ScreenParams.xy;
                float2 pixelatedUV = floor(uv * screenSize / _PixelSize) * _PixelSize / screenSize;

                // Используем PointClamp для чётких пикселей
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV);

                // 2. Квантование цветов
                color.rgb = floor(color.rgb * _ColorLevels) / _ColorLevels;

                // 3. Дизеринг
                float2 pixelPos = floor(uv * screenSize / _PixelSize);
                uint x = (uint)pixelPos.x % 4;
                uint y = (uint)pixelPos.y % 4;
                float ditherValue = DITHER_MATRIX[y * 4 + x] / 16.0;

                // Применяем дизеринг до финального квантования
                color.rgb += (ditherValue - 0.5) / _ColorLevels * _DitherStrength;

                // Финальное квантование для чистых цветов
                color.rgb = floor(color.rgb * _ColorLevels + 0.5) / _ColorLevels;

                return color;
            }
            ENDHLSL
        }
    }
}