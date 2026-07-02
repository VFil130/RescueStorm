Shader "Hidden/StaticGrain"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0.2
        _GrainSize ("Grain Size", Range(0.5, 5.0)) = 1.5
        _LuminanceInfluence ("Luminance Influence", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "StaticGrainPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float _Intensity;
            float _GrainSize;
            float _LuminanceInfluence;

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(output.uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
                return output;
            }

            // Хеш-функция для заданной целочисленной точки решётки
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // Value noise с гладкой интерполяцией
            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                // Кубическая интерполяция для плавности
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Многооктавный value noise – создаёт зёрна разного размера
            float FilmGrainNoise(float2 uv, float size)
            {
                float value = 0.0;
                float amplitude = 1.0;
                float frequency = size;
                float maxValue = 0.0;

                // 4 октавы
                for (int i = 0; i < 4; i++)
                {
                    value += ValueNoise(uv * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.6;
                    frequency *= 2.0;
                }

                return value / maxValue; // нормализуем в [0..1]
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv);

                // Вычисляем шум в координатах пикселей экрана, масштаб зависит от _GrainSize
                float2 pixelCoord = input.uv * _BlitTexture_TexelSize.zw;
                // _GrainSize управляет базовой частотой: больше размер -> меньше частота
                float noise = FilmGrainNoise(pixelCoord / _GrainSize, 1.0);

                // Центрируем вокруг 0.5
                float grain = (noise - 0.5) * _Intensity;

                // Зависимость от яркости: чем ярче пиксель, тем слабее зерно
                float luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
                float lumFactor = lerp(1.0, 1.0 - luminance, _LuminanceInfluence);
                grain *= lumFactor;

                color.rgb += grain;
                return color;
            }
            ENDHLSL
        }
    }
}