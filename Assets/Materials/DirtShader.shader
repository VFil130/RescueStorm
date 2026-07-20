// Triplanar PBR Shader - 2 Layers with Separate Noises, Transparent with Min Alpha for Reflections
Shader "Custom/TriplanarPBR_2Layers_SeparateNoise_TransparentReflections"
{
    Properties
    {
        // Layer 1
        [Header(Layer 1)]
        [NoScaleOffset] _Layer1_BaseMap   ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset] _Layer1_NormalMap ("Normal", 2D) = "bump" {}

        // Layer 2
        [Header(Layer 2)]
        [NoScaleOffset] _Layer2_BaseMap   ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset] _Layer2_NormalMap ("Normal", 2D) = "bump" {}

        // Triplanar settings
        [Header(Triplanar)]
        _TriplanarScale   ("Texture Scale", Float) = 1.0
        _BlendSharpness   ("Triplanar Blend Sharpness", Range(1, 10)) = 2.0

        // Noise for blending layers (albedo)
        [Header(Albedo Blend Noise)]
        _NoiseScale       ("Albedo Noise Scale", Float) = 2.0
        _NoiseContrast    ("Albedo Noise Contrast", Range(0.2, 5.0)) = 1.0
        _NoiseThreshold   ("Albedo Noise Threshold", Range(0,1)) = 0.5
        _NoiseHardness    ("Albedo Noise Hardness", Range(0,1)) = 0.0

        // Noise for blending PBR (normals)
        [Header(PBR Blend Noise)]
        _PBRNoiseScale    ("PBR Noise Scale", Float) = 2.0
        _PBRNoiseContrast ("PBR Noise Contrast", Range(0.2, 5.0)) = 1.0
        _PBRNoiseThreshold("PBR Noise Threshold", Range(0,1)) = 0.5
        _PBRNoiseHardness ("PBR Noise Hardness", Range(0,1)) = 0.0

        // Noise for alpha cutout (to break tiling)
        [Header(Alpha Cutout Noise)]
        _AlphaNoiseScale  ("Alpha Noise Scale", Float) = 2.0
        _AlphaNoiseMin    ("Alpha Noise Min", Range(0,1)) = 0.0
        _AlphaNoiseMax    ("Alpha Noise Max", Range(0,1)) = 1.0
        [Toggle] _AlphaInvert ("Invert Alpha Noise", Float) = 0

        // Normal map settings
        [Header(Normal Maps)]
        _NormalScale      ("Normal Scale", Range(0, 2)) = 1.0

        // Metallic and Smoothness
        [Header(Metallic And Smoothness)]
        _Metallic         ("Metallic", Range(0,1)) = 0.0
        _Smoothness       ("Smoothness", Range(0,1)) = 0.5

        // Alpha
        [Header(Alpha)]
        _AlphaMultiplier  ("Alpha Multiplier", Range(0,1)) = 1.0
        _MinAlpha         ("Minimum Alpha (for reflections)", Range(0, 0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords (minimal)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_Layer1_BaseMap);   SAMPLER(sampler_Layer1_BaseMap);
            TEXTURE2D(_Layer1_NormalMap); SAMPLER(sampler_Layer1_NormalMap);
            TEXTURE2D(_Layer2_BaseMap);   SAMPLER(sampler_Layer2_BaseMap);
            TEXTURE2D(_Layer2_NormalMap); SAMPLER(sampler_Layer2_NormalMap);

            float _TriplanarScale;
            float _BlendSharpness;
            float _NoiseScale;
            float _NoiseContrast;
            float _NoiseThreshold;
            float _NoiseHardness;
            float _PBRNoiseScale;
            float _PBRNoiseContrast;
            float _PBRNoiseThreshold;
            float _PBRNoiseHardness;
            float _AlphaNoiseScale;
            float _AlphaNoiseMin;
            float _AlphaNoiseMax;
            float _AlphaInvert;
            float _NormalScale;
            float _Metallic;
            float _Smoothness;
            float _AlphaMultiplier;
            float _MinAlpha;

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float SmoothNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x),
                         lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                         lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z
                );
            }

            float4 TriplanarSample(TEXTURE2D_PARAM(tex, samplerTex), float3 position, float3 normal, float scale, float blendSharpness)
            {
                float4 xProj = SAMPLE_TEXTURE2D(tex, samplerTex, position.zy * scale);
                float4 yProj = SAMPLE_TEXTURE2D(tex, samplerTex, position.xz * scale);
                float4 zProj = SAMPLE_TEXTURE2D(tex, samplerTex, position.xy * scale);

                float3 weights = pow(abs(normal), blendSharpness);
                weights = weights / (weights.x + weights.y + weights.z);

                return xProj * weights.x + yProj * weights.y + zProj * weights.z;
            }

            float3 UnpackNormal(float4 packedNormal, float scale)
            {
                #if defined(UNITY_NO_DXT5nm)
                    float3 normal = packedNormal.rgb * 2.0 - 1.0;
                #else
                    float3 normal;
                    normal.xy = packedNormal.ag * 2.0 - 1.0;
                    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
                #endif
                normal.xy *= scale;
                normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
                return normal;
            }

            float3 TangentToWorld(float3 tangentNormal, float3 normal, float3 tangent, float3 bitangent)
            {
                float3x3 TBN = float3x3(tangent, bitangent, normal);
                return normalize(mul(tangentNormal, TBN));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 worldPos = input.positionWS;
                float3 worldNormal = normalize(input.normalWS);
                float3 worldTangent = normalize(input.tangentWS.xyz);
                float3 worldBitangent = normalize(cross(worldNormal, worldTangent) * input.tangentWS.w);

                // Sample Layer1
                float4 albedo1 = TriplanarSample(TEXTURE2D_ARGS(_Layer1_BaseMap, sampler_Layer1_BaseMap), worldPos, worldNormal, _TriplanarScale, _BlendSharpness);
                float4 normal1_packed = TriplanarSample(TEXTURE2D_ARGS(_Layer1_NormalMap, sampler_Layer1_NormalMap), worldPos, worldNormal, _TriplanarScale, _BlendSharpness);

                // Sample Layer2
                float4 albedo2 = TriplanarSample(TEXTURE2D_ARGS(_Layer2_BaseMap, sampler_Layer2_BaseMap), worldPos, worldNormal, _TriplanarScale, _BlendSharpness);
                float4 normal2_packed = TriplanarSample(TEXTURE2D_ARGS(_Layer2_NormalMap, sampler_Layer2_NormalMap), worldPos, worldNormal, _TriplanarScale, _BlendSharpness);

                // Convert normals to world space
                float3 tangentNormal1 = UnpackNormal(normal1_packed, _NormalScale);
                float3 worldNormal1 = TangentToWorld(tangentNormal1, worldNormal, worldTangent, worldBitangent);
                float3 tangentNormal2 = UnpackNormal(normal2_packed, _NormalScale);
                float3 worldNormal2 = TangentToWorld(tangentNormal2, worldNormal, worldTangent, worldBitangent);

                // Albedo blend noise
                float albedoNoise = SmoothNoise(worldPos * _NoiseScale);
                albedoNoise = saturate(pow(albedoNoise, _NoiseContrast));
                float albedoStepped = step(_NoiseThreshold, albedoNoise);
                albedoNoise = lerp(albedoNoise, albedoStepped, _NoiseHardness);

                // PBR blend noise (for normals)
                float pbrNoise = SmoothNoise(worldPos * _PBRNoiseScale);
                pbrNoise = saturate(pow(pbrNoise, _PBRNoiseContrast));
                float pbrStepped = step(_PBRNoiseThreshold, pbrNoise);
                pbrNoise = lerp(pbrNoise, pbrStepped, _PBRNoiseHardness);

                // Blend albedo and normals
                float4 finalAlbedo = lerp(albedo1, albedo2, albedoNoise);
                float3 finalNormal = lerp(worldNormal1, worldNormal2, pbrNoise);
                finalNormal = normalize(finalNormal);

                // Alpha cutout noise
                float alphaNoise = SmoothNoise(worldPos * _AlphaNoiseScale);
                if (_AlphaInvert)
                    alphaNoise = 1.0 - alphaNoise;
                float alphaFactor = lerp(_AlphaNoiseMin, _AlphaNoiseMax, alphaNoise);

                // Final alpha with minimum clamp
                float alpha = finalAlbedo.a * _AlphaMultiplier * alphaFactor;
                alpha = max(alpha, _MinAlpha); // гарантируем минимальную альфу для отражений

                // PBR setup
                InputData inputData = (InputData)0;
                inputData.positionWS = worldPos;
                inputData.normalWS = finalNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(worldPos);
                inputData.shadowCoord = TransformWorldToShadowCoord(worldPos);
                inputData.fogCoord = 0;
                inputData.vertexLighting = 0;
                inputData.bakedGI = 0;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = 1;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo.rgb;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.occlusion = 1.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.specular = half3(0,0,0);
                surfaceData.normalTS = half3(0,0,1);
                surfaceData.emission = 0;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.a = alpha;
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}