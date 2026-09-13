Shader "Tutorial/VolumetricLightShafts"
{
    Properties
    {
        _MaxDistance("Max distance", float) = 100
        _StepSize("Step size", Range(0.05, 5)) = 0.5
        _DensityMultiplier("Density multiplier", Range(0, 10)) = 1

        [KeywordEnum(Spatial, Temporal, Off)] _JitterMode("Jitter mode", Float) = 0
        _JitterAmount("Jitter amount", Range(0, 2)) = 1

        _NoiseTiling("Noise tiling", float) = 1
        _NoiseContrast("Noise contrast", Range(0.1, 5)) = 2
        _DensityThreshold("Density threshold", Range(0, 1)) = 0.3

        [HDR]_LightContribution("Light contribution (HDR)", Color) = (1, 1, 1, 1)
        _LightScattering("Light scattering (g)", Range(0, 0.95)) = 0.5
        _Intensity("Intensity", Range(0, 10)) = 1
        _MaxScatter("Max scatter per step", Range(0, 10)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #pragma shader_feature_local _JITTERMODE_SPATIAL _JITTERMODE_TEMPORAL _JITTERMODE_OFF

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float  _MaxDistance;
            float  _StepSize;
            float  _DensityMultiplier;
            float  _JitterAmount;
            float  _NoiseTiling;
            float  _NoiseContrast;
            float  _DensityThreshold;
            float4 _LightContribution;
            float  _LightScattering;
            float  _Intensity;
            float  _MaxScatter;

            // ---------- Шум для плотности ----------

            float hash31(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0, 0, 0));
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float fbm3D(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += a * valueNoise3D(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            // ---------- Джиттер ----------

            float spatial_dither(float2 pixelCoords)
            {
                // Стабильный, детерминированный по пикселю. Не «шумит» при движении.
                float n = hash31(float3(pixelCoords, 0.123));
                return n;
            }

            float temporal_dither(float2 pixelCoords)
            {
                return InterleavedGradientNoise(
                    pixelCoords,
                    (int)(_Time.y / max(HALF_EPS, unity_DeltaTime.x))
                );
            }

            float get_jitter(float2 pixelCoords)
            {
            #if defined(_JITTERMODE_OFF)
                return 0.0;
            #elif defined(_JITTERMODE_TEMPORAL)
                return temporal_dither(pixelCoords) * _JitterAmount;
            #else
                return spatial_dither(pixelCoords) * _JitterAmount;
            #endif
            }

            // ---------- Вспомогательное ----------

            float henyey_greenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) /
                    (4.0 * PI * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5f));
            }

            float get_dust_density(float3 worldPos)
            {
                float3 p = worldPos * 0.01 * _NoiseTiling;
                float n = fbm3D(p);
                n = saturate((n - 0.5) * _NoiseContrast + 0.5);
                return saturate(n - _DensityThreshold) * _DensityMultiplier;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 col   = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                float  depth = SampleSceneDepth(IN.texcoord);
                float3 worldPos = ComputeWorldSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_VP);

                float3 entryPoint = _WorldSpaceCameraPos;
                float3 viewDir    = worldPos - _WorldSpaceCameraPos;
                float  viewLength = length(viewDir);
                float3 rayDir     = viewDir / max(viewLength, 1e-5);

                float2 pixelCoords = IN.texcoord * _BlitTexture_TexelSize.zw;
                float  distLimit   = min(viewLength, _MaxDistance);

                float distTravelled = get_jitter(pixelCoords) * _StepSize;

                float3 scatter = 0;

            #ifdef _ADDITIONAL_LIGHTS
                InputData inputData = (InputData)0;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.positionCS = IN.positionCS;

                uint lightsCount = GetAdditionalLightsCount();
            #endif

                while (distTravelled < distLimit)
                {
                    float3 rayPos = entryPoint + rayDir * distTravelled;
                    float  dust   = get_dust_density(rayPos);

                    if (dust > 0)
                    {
                        float stepAtten = dust * _StepSize;

                    #ifdef _ADDITIONAL_LIGHTS
                        inputData.positionWS = rayPos;
                        inputData.normalWS   = -rayDir;

                        LIGHT_LOOP_BEGIN(lightsCount)
                            Light light = GetAdditionalLight(lightIndex, rayPos);

                            float atten = light.distanceAttenuation * light.shadowAttenuation;
                            if (atten > 0.0)
                            {
                                float cosTheta = dot(rayDir, light.direction);
                                float phase    = henyey_greenstein(cosTheta, _LightScattering);

                                float3 contrib = light.color
                                               * _LightContribution.rgb
                                               * phase
                                               * atten
                                               * stepAtten;

                                // Clamp спасает от HDR-выбросов (зелёно-розовые пиксели
                                // у основания луча — это именно они, до тональной компрессии).
                                contrib = min(contrib, _MaxScatter);

                                scatter += contrib;
                            }
                        LIGHT_LOOP_END
                    #endif
                    }

                    distTravelled += _StepSize;
                }

                scatter *= _Intensity;
                scatter = min(scatter, _MaxScatter * 4.0); // общий клэмп на выходе

                // Финальная защита от NaN/Inf в очень тёмных/пересвеченных пикселях
                if (any(isnan(scatter)) || any(isinf(scatter)))
                    scatter = 0;

                return half4(col.rgb + scatter, col.a);
            }
            ENDHLSL
        }
    }
}