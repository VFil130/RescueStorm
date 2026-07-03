Shader "Hidden/StaticGrain"
{
    Properties
    {
        _Intensity ("Grain Intensity", Range(0, 1)) = 0.25
        _GrainSize ("Grain Size", Range(0.5, 5.0)) = 1.5
        [NoScaleOffset] _NoiseTex ("Noise Texture (optional)", 2D) = "white" {}

        [Toggle] _DOFEnable ("Depth of Field + Blur", Float) = 0
        _FocusDistance ("Focus Distance (m)", Range(0.1, 100)) = 5.0
        _FocusRange ("Focus Range (m)", Range(0.1, 20)) = 2.0
        _BlurRadius ("Max Blur Radius (pixels)", Range(0, 8)) = 4.0
        _FocusGrainMin ("Grain in Focus (multiplier)", Range(0, 2)) = 0.2
        _FocusGrainMax ("Grain out of Focus (multiplier)", Range(0, 2)) = 1.0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            float4 _NoiseTex_TexelSize;

            float _Intensity;
            float _GrainSize;
            float _DOFEnable;
            float _FocusDistance;
            float _FocusRange;
            float _BlurRadius;
            float _FocusGrainMin;
            float _FocusGrainMax;

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(output.uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
                return output;
            }

            // ---------- Улучшенный процедурный шум (мягкий) ----------
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // Шум с плавной интерполяцией (value noise)
            float SmoothNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1,0));
                float c = Hash(i + float2(0,1));
                float d = Hash(i + float2(1,1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Многооктавный шум с заниженным контрастом (имитация гауссова распределения)
            float ProceduralGrain(float2 uv, float grainSize)
            {
                float2 pixel = uv * _BlitTexture_TexelSize.zw;
                // Разрушаем повторяемость
                float2 seed = floor(pixel * 0.01) * 100.0;
                float value = 0.0;
                float amp = 1.0;
                float freq = 0.8 / max(grainSize, 0.001);
                float maxVal = 0.0;
                for (int i = 0; i < 5; i++)
                {
                    // Смещаем UV на случайную величину, чтобы каждая октава была уникальной
                    float2 offset = float2(Hash(seed + i), Hash(seed + i + 10.0)) * 10.0;
                    float n = SmoothNoise(uv * freq + offset);
                    // Уменьшаем амплитуду для низкоконтрастного шума
                    value += (n - 0.5) * amp;
                    maxVal += amp;
                    amp *= 0.55;
                    freq *= 2.0;
                }
                // Возвращаем значение в диапазоне [-0.5, 0.5], добавляем 0.5 для центрирования
                return (value / maxVal) * 0.7 + 0.5; // 0.7 снижает контраст
            }

            // ---------- Текстурный шум (идентичен FilmGrain) ----------
            float TextureGrain(float2 uv, float grainSize)
            {
                float scale = 1.0 / max(grainSize, 0.001);
                float2 texUV = uv * scale;
                // Билинейная фильтрация сглаживает шум
                return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, texUV).r;
            }
            // ------------------------------------------------------------

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv);

                // 1. DOF фактор
                float dofFactor = 0.0;
                if (_DOFEnable > 0.5)
                {
                    float deviceDepth = SampleSceneDepth(input.uv);
                    if (deviceDepth > 0.0)
                    {
                        float eyeDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);
                        float distFromFocus = abs(eyeDepth - _FocusDistance);
                        dofFactor = smoothstep(0.0, _FocusRange, distFromFocus);
                    }
                    else dofFactor = 1.0;
                }

                // 2. Размытие
                if (_DOFEnable > 0.5 && dofFactor > 0.001)
                {
                    float2 texelSize = _BlitTexture_TexelSize.xy;
                    float blurRadius = _BlurRadius * dofFactor;
                    half3 blurred = half3(0,0,0);
                    float weight = 0.0;
                    for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x,y) * texelSize * blurRadius;
                        blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv + offset).rgb;
                        weight += 1.0;
                    }
                    color.rgb = blurred / weight;
                }

                // 3. Зерно
                float noise;
                // Проверяем, назначена ли текстура (если нет, она будет белой, её texelSize.x = 1/ширина)
                bool hasTexture = _NoiseTex_TexelSize.x < 0.99; // белая дефолтная 4x4 имеет texelSize 0.25
                if (hasTexture)
                {
                    noise = TextureGrain(input.uv, _GrainSize);
                }
                else
                {
                    noise = ProceduralGrain(input.uv, _GrainSize);
                }

                float grain = (noise - 0.5) * _Intensity;

                // 4. Модуляция по фокусу
                if (_DOFEnable > 0.5)
                {
                    float grainMult = lerp(_FocusGrainMin, _FocusGrainMax, dofFactor);
                    grain *= grainMult;
                }

                color.rgb += grain;
                return color;
            }
            ENDHLSL
        }
    }
}