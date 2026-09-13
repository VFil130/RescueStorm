Shader "Tutorial/VolumetricLightShafts"
{
    Properties
    {
        _MaxDistance("Max distance", float) = 100
        _StepSize("Step size", Range(0.05, 5)) = 0.5
        _DensityMultiplier("Density multiplier", Range(0, 10)) = 1
        _JitterAmount("Jitter amount", Range(0, 2)) = 1

        _NoiseTiling("Noise tiling", float) = 1
        _NoiseContrast("Noise contrast", Range(0.1, 5)) = 2
        _DensityThreshold("Density threshold", Range(0, 1)) = 0.3
        _NoiseSpeed("Noise speed", Range(0, 0.1)) = 0.008

        [HDR]_LightContribution("Light contribution (HDR)", Color) = (1, 1, 1, 1)
        _LightScattering("Light scattering (g)", Range(0, 0.95)) = 0.5
        _Intensity("Intensity", Range(0, 10)) = 1
        _MaxScatter("Max scatter per step", Range(0, 10)) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "VolumetricLightShafts"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_LAYERS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_STEPS 128
            #define NO_DUST_LAYER_BIT 1u   // Бит 0 = первый Rendering Layer

            float  _MaxDistance;
            float  _StepSize;
            float  _DensityMultiplier;
            float  _JitterAmount;
            float  _NoiseTiling;
            float  _NoiseContrast;
            float  _DensityThreshold;
            float  _NoiseSpeed;
            float4 _LightContribution;
            float  _LightScattering;
            float  _Intensity;
            float  _MaxScatter;

            uint _AdditionalLightCount;

            // ---------------- Процедурный шум ----------------

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

                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));

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

            // Шумная плотность — «клочки пыли».
            float get_dust_density_noisy(float3 worldPos)
            {
                float3 p = worldPos * 0.01 * _NoiseTiling;
                p += float3(_Time.y * _NoiseSpeed, 0.0, _Time.y * _NoiseSpeed * 0.6);
                float n = fbm3D(p);
                n = saturate((n - 0.5) * _NoiseContrast + 0.5);
                return saturate(n - _DensityThreshold) * _DensityMultiplier;
            }

            // Чистая плотность — ровный объёмный свет.
            // Коэффициент 0.25 компенсирует среднее fBm (~0.2–0.25),
            // чтобы визуальная плотность совпадала с шумным режимом.
            float get_dust_density_clean()
            {
                return _DensityMultiplier * 0.25;
            }

            // ---------------- Фазовая функция ----------------

            float henyey_greenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) /
                    (4.0 * PI * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5f));
            }

            // ---------------- Фрагмент ----------------

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uvSS = IN.texcoord;
                float3 col  = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvSS).rgb;

                float rawDepth = SampleSceneDepth(uvSS);
                float4 positionCS = ComputeClipSpacePosition(uvSS, rawDepth);
                float4 positionVS = mul(UNITY_MATRIX_I_P, positionCS);
                positionVS.xyz /= positionVS.w;

                float3 rayOriginVS;
                float3 dirToSurfaceVS;
                float  distanceToSurfaceWS;

                if (unity_OrthoParams.w == 1.0)
                {
                    rayOriginVS         = float3(positionVS.xy, 0.0);
                    dirToSurfaceVS      = float3(0.0, 0.0, -1.0);
                    distanceToSurfaceWS = abs(positionVS.z);
                }
                else
                {
                    rayOriginVS         = float3(0.0, 0.0, 0.0);
                    dirToSurfaceVS      = normalize(positionVS.xyz);
                    distanceToSurfaceWS = length(positionVS.xyz);
                }

                float3 cameraPositionWS     = GetCameraPositionWS();
                float3 rayOriginWS          = cameraPositionWS + mul((float3x3)UNITY_MATRIX_I_V, rayOriginVS);
                float3 directionToSurfaceWS = mul((float3x3)UNITY_MATRIX_I_V, dirToSurfaceVS);

                float raymarchDistance = min(distanceToSurfaceWS, _MaxDistance);

                int steps = (int)clamp(ceil(raymarchDistance / max(_StepSize, 0.01)), 1, MAX_STEPS);
                float stepWS = raymarchDistance / (float)steps;

                float2 uvPP  = uvSS * _ScaledScreenParams.xy;
                int    frame = (int)(_Time.y * 60.0) % 60;
                float  ign   = InterleavedGradientNoise(uvPP, frame);
                float  stepNoise = stepWS * ign * _JitterAmount;

                float3 lighting = 0;
                float  transmittance = 1;

                InputData inputData = (InputData)0;
                inputData.normalizedScreenSpaceUV = uvSS;
                inputData.positionCS = IN.positionCS;
                inputData.viewDirectionWS = -directionToSurfaceWS;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float distWS = (float)i * stepWS + stepNoise;
                    if (distWS > raymarchDistance) break;

                    float3 posWS = rayOriginWS + directionToSurfaceWS * distWS;

                    // Считаем обе плотности один раз на шаг.
                    float dustNoisy = get_dust_density_noisy(posWS);
                    float dustClean = get_dust_density_clean();

                    // Обе нулевые — пропускаем (бывает только при DensityMultiplier = 0).
                    if (dustNoisy < 0.001 && dustClean < 0.001) continue;

                    // Transmittance — это физическое свойство среды, поэтому
                    // используем «основную» плотность (шумную). Она модулирует,
                    // сколько света от дальних шагов доходит до камеры.
                    float densityPerStep    = dustNoisy * stepWS;
                    float stepTransmittance = exp(-densityPerStep);

                    float3 stepLighting = 0;

                    inputData.positionWS = posWS;
                    inputData.normalWS   = -directionToSurfaceWS;

                    LIGHT_LOOP_BEGIN(_AdditionalLightCount)
                        Light light = GetAdditionalPerObjectLight(lightIndex, posWS);
                        light.shadowAttenuation = 1.0;

                    #ifdef _ADDITIONAL_LIGHT_SHADOWS
                        light.shadowAttenuation = AdditionalLightShadow(
                            lightIndex,
                            posWS,
                            light.direction,
                            half4(1, 1, 1, 1),
                            _AdditionalLightsOcclusionProbes[lightIndex]
                        );
                    #endif

                        if (light.distanceAttenuation < 0.001) continue;
                        if (light.shadowAttenuation   < 0.001) continue;

                        // Проверяем бит Rendering Layer.
                        // Если установлен — эта лампа без шума (чистый конус).
                        bool noDust = false;
                    #if defined(_LIGHT_LAYERS)
                        noDust = (light.layerMask & NO_DUST_LAYER_BIT) != 0u;
                    #endif

                        float dust = noDust ? dustClean : dustNoisy;

                        float cosTheta = dot(directionToSurfaceWS, light.direction);
                        float phase    = henyey_greenstein(cosTheta, _LightScattering);

                        // Здесь вместо общего stepScattering используем
                        // плотность конкретно этой лампы, умноженную на шаг.
                        stepLighting += light.color
                                      * _LightContribution.rgb
                                      * phase
                                      * light.distanceAttenuation
                                      * light.shadowAttenuation
                                      * dust
                                      * stepWS;
                    LIGHT_LOOP_END

                    stepLighting = min(stepLighting, _MaxScatter);

                    lighting     += stepLighting * transmittance;
                    transmittance *= stepTransmittance;

                    if (transmittance < 0.001)
                    {
                        transmittance = 0.0;
                        break;
                    }
                }

                lighting *= _Intensity;
                lighting  = min(lighting, _MaxScatter * 4.0);

                if (any(isnan(lighting)) || any(isinf(lighting)))
                    lighting = 0;

                return half4(col + lighting, 1);
            }
            ENDHLSL
        }
    }
}