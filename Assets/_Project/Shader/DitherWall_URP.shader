Shader "Custom/DitherWall_URP"
{
    Properties
    {
        [Header(Surface Inputs)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle(_NORMALMAP)] _EnableNormalMap("Enable Normal Map", Float) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Toggle(_METALLICSPECGLOSSMAP)] _EnableMetallicMap("Enable Metallic Map", Float) = 0.0
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Toggle(_OCCLUSIONMAP)] _EnableOcclusionMap("Enable Occlusion Map", Float) = 0.0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [Toggle(_EMISSION)] _EnableEmission("Enable Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "black" {}

        [Header(Dither Camera Proximity Fade)]
        [Toggle] _EnableDitherFade("Ativar Efeito Dither Fade", Float) = 1.0
        _FadeNearDistance("Distancia Minima (100% Invisivel)", Float) = 1.0
        _FadeFarDistance("Distancia Maxima (Totalmente Solido)", Float) = 2.5
        _FadeOffsetY("Ajuste de Altura do Fade no Eixo Y (Subir ou Descer)", Range(-3.0, 6.0)) = 1.5
        _VerticalDistanceWeight("Peso Vertical da Parede (0 = Reto Vertical, 1 = Diagonal)", Range(0.0, 1.0)) = 0.2
        _MinDitherAlpha("Alpha Minimo no Ponto Proximo (0 = Invisivel)", Range(0.0, 0.4)) = 0.0
        _DitherDotSize("Tamanho dos Pontinhos (1 = Fino, 2-3 = Textura Bonita, 4 = Retro)", Range(1.0, 6.0)) = 3.0
        _DitherPattern("Padrao Dither (0 = Bayer 8x8 Artistico, 1 = Bayer 4x4 Marcante)", Range(0.0, 1.0)) = 0.0

        [Header(Foco no Player)]
        [Toggle] _EnablePlayerFade("Ativar Recorte ao Redor do Player", Float) = 1.0
        _PlayerFadeRadius("Raio ao Redor do Player", Float) = 2.2
        _PlayerFadeFeather("Suavidade da Borda do Player", Float) = 1.0
        _PlayerYOffset("Deslocar Altura do Player no Eixo Y", Range(-2.0, 4.0)) = 0.5
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal"
        }

        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }

        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _BumpScale;
            half _Metallic;
            half _Smoothness;
            half _OcclusionStrength;
            float _FadeNearDistance;
            float _FadeFarDistance;
            float _FadeOffsetY;
            float _VerticalDistanceWeight;
            float _MinDitherAlpha;
            float _DitherDotSize;
            float _DitherPattern;
            float _EnableDitherFade;
            float _EnablePlayerFade;
            float _PlayerFadeRadius;
            float _PlayerFadeFeather;
            float _PlayerYOffset;
        CBUFFER_END

        // Posicao global do Player (atualizada por script via Shader.SetGlobalVector)
        float4 _PlayerPos;

        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap);    SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap);        SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_EmissionMap);         SAMPLER(sampler_EmissionMap);

        // Matriz de Bayer 8x8 (64 niveis de transicao suave, nitida e estetica)
        float GetBayer8x8(float2 screenPos, float dotSize)
        {
            static const float bayer8[64] = {
                 0.0 / 64.0, 32.0 / 64.0,  8.0 / 64.0, 40.0 / 64.0,  2.0 / 64.0, 34.0 / 64.0, 10.0 / 64.0, 42.0 / 64.0,
                48.0 / 64.0, 16.0 / 64.0, 56.0 / 64.0, 24.0 / 64.0, 50.0 / 64.0, 18.0 / 64.0, 58.0 / 64.0, 26.0 / 64.0,
                12.0 / 64.0, 44.0 / 64.0,  4.0 / 64.0, 36.0 / 64.0, 14.0 / 64.0, 46.0 / 64.0,  6.0 / 64.0, 38.0 / 64.0,
                60.0 / 64.0, 28.0 / 64.0, 52.0 / 64.0, 20.0 / 64.0, 62.0 / 64.0, 30.0 / 64.0, 54.0 / 64.0, 22.0 / 64.0,
                 3.0 / 64.0, 35.0 / 64.0, 11.0 / 64.0, 43.0 / 64.0,  1.0 / 64.0, 33.0 / 64.0,  9.0 / 64.0, 41.0 / 64.0,
                51.0 / 64.0, 19.0 / 64.0, 59.0 / 64.0, 27.0 / 64.0, 49.0 / 64.0, 17.0 / 64.0, 57.0 / 64.0, 25.0 / 64.0,
                15.0 / 64.0, 47.0 / 64.0,  7.0 / 64.0, 39.0 / 64.0, 13.0 / 64.0, 45.0 / 64.0,  5.0 / 64.0, 37.0 / 64.0,
                63.0 / 64.0, 31.0 / 64.0, 55.0 / 64.0, 23.0 / 64.0, 61.0 / 64.0, 29.0 / 64.0, 53.0 / 64.0, 21.0 / 64.0
            };

            uint s = max(1u, (uint)dotSize);
            uint2 pixelCoord = uint2(screenPos);
            uint2 coord = (pixelCoord / s) % 8u;
            return bayer8[coord.x + coord.y * 8u];
        }

        // Matriz de Bayer 4x4 (16 niveis de dithering retro classico)
        float GetBayer4x4(float2 screenPos, float dotSize)
        {
            static const float bayer4[16] = {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            uint s = max(1u, (uint)dotSize);
            uint2 pixelCoord = uint2(screenPos);
            uint2 coord = (pixelCoord / s) % 4u;
            return bayer4[coord.x + coord.y * 4u];
        }

        void ApplyDitherClip(float4 positionCS, float3 positionWS)
        {
            if (_EnableDitherFade < 0.5) return;

            float finalDitherAlpha = 1.0;

            // 1. Proximidade da Camera com ajuste direto de Altura Y
            float effectiveY = positionWS.y - _FadeOffsetY;
            float3 camDiff = float3(positionWS.x - _WorldSpaceCameraPos.x, effectiveY - _WorldSpaceCameraPos.y, positionWS.z - _WorldSpaceCameraPos.z);
            float camDist = length(float3(camDiff.x, camDiff.y * _VerticalDistanceWeight, camDiff.z));

            if (camDist < _FadeFarDistance)
            {
                float t = saturate((camDist - _FadeNearDistance) / max(0.01, (_FadeFarDistance - _FadeNearDistance)));
                float fade = smoothstep(0.0, 1.0, t);
                float camAlpha = lerp(_MinDitherAlpha, 1.0, fade);
                finalDitherAlpha = min(finalDitherAlpha, camAlpha);
            }

            // 2. Foco no Player: Cilindro de Visibilidade ao redor do Astronauta
            // Apenas paredes que ficam entre a camera e o jogador desaparecem!
            // Paredes que estao atras do jogador permanecem 100% solidas.
            if (_EnablePlayerFade > 0.5 && _PlayerPos.w > 0.5)
            {
                float3 camToPlayer = _PlayerPos.xyz - _WorldSpaceCameraPos.xyz;
                float3 camToWall = positionWS - _WorldSpaceCameraPos.xyz;
                float distCamToPlayer = length(camToPlayer);
                float distAlongView = dot(camToWall, camToPlayer) / max(0.001, distCamToPlayer);

                // So ativa se a parede estiver a frente do player (mais perto da camera)
                if (distAlongView <= (distCamToPlayer + 0.5))
                {
                    float horizDist = length(positionWS.xz - _PlayerPos.xz);
                    float playerFocusY = _PlayerPos.y + _PlayerYOffset;
                    float yDiff = positionWS.y - playerFocusY;

                    if (yDiff > -1.5 && yDiff < 5.0)
                    {
                        float pt = saturate((horizDist - _PlayerFadeRadius) / max(0.01, _PlayerFadeFeather));
                        float pFade = smoothstep(0.0, 1.0, pt);
                        float playerAlpha = lerp(_MinDitherAlpha, 1.0, pFade);
                        finalDitherAlpha = min(finalDitherAlpha, playerAlpha);
                    }
                }
            }

            if (finalDitherAlpha >= 0.999) return;

            if (finalDitherAlpha <= 0.001)
            {
                clip(-1.0);
                return;
    
            }

            float bayerVal = (_DitherPattern > 0.5) 
                ? GetBayer4x4(positionCS.xy, _DitherDotSize) 
                : GetBayer8x8(positionCS.xy, _DitherDotSize);

            clip(finalDitherAlpha - bayerVal);
        }
        ENDHLSL

        // =================================================================
        // PASS 1: FORWARD LIT (PBR, Luz Principal, Luzes Adicionais, Sombras)
        // =================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD2;
                float2 uv           : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // --- 1. DITHER FADING ---
                ApplyDitherClip(input.positionCS, input.positionWS);

                // --- 2. MATERIAL PROPERTIES ---
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                #if defined(_NORMALMAP)
                    half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                    half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                #else
                    half3 normalWS = normalize(input.normalWS);
                #endif

                half metallic = _Metallic;
                half smoothness = _Smoothness;
                #if defined(_METALLICSPECGLOSSMAP)
                    half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                    metallic = metallicGloss.r * _Metallic;
                    smoothness = metallicGloss.a * _Smoothness;
                #endif

                half occlusion = 1.0;
                #if defined(_OCCLUSIONMAP)
                    occlusion = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
                #endif

                half3 emission = half3(0, 0, 0);
                #if defined(_EMISSION)
                    emission = _EmissionColor.rgb * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                #endif

                // --- 3. PBR LIGHTING SETUP ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = occlusion;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 2: SHADOW CASTER (Dissolve as sombras também!)
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.positionWS = positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyDitherClip(input.positionCS, input.positionWS);
                return 0;
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 3: DEPTH ONLY (Para efeitos de profundidade e SSAO)
        // =================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyDitherClip(input.positionCS, input.positionWS);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
