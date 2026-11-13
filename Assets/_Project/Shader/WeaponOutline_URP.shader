Shader "Custom/WeaponOutline_URP"
{
    Properties
    {
        [Header(Outline Settings)]
        [HDR] _OutlineColor("Cor do Outline (HDR)", Color) = (2.0, 1.6, 0.25, 1.0)
        _OutlineWidth("Espessura do Outline", Range(0.001, 0.05)) = 0.012
        _PulseSpeed("Velocidade do Pulso", Range(0.0, 10.0)) = 3.0
        _PulseIntensity("Intensidade do Pulso", Range(0.0, 1.0)) = 0.25
        _Alpha("Transparencia Global", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal"
        }

        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-20"
            "IgnoreProjector" = "True"
        }

        LOD 200

        // =========================================================================
        // PASS UNICO: OUTLINE DE SILHUETA EXTERNA (ESTRITAMENTE NO FUNDO)
        // Renderiza apenas as faces traseiras (Cull Front), infladas suavemente ao longo
        // das normais e empurradas para o fundo no depth buffer (+Z clip).
        // A malha frontal original da arma fica 100% limpa, visivel e na frente.
        // =========================================================================
        Pass
        {
            Name "OutlinePass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _PulseSpeed;
                float _PulseIntensity;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // Extrusao uniforme no espaco de mundo ao longo da normal da superficie
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz) + (normalWS * _OutlineWidth);

                // Converte posicao inflada para Clip Space
                output.positionCS = TransformWorldToHClip(posWS);

                // Empurra ligeiramente para o fundo no depth buffer (para tras da superficie frontal)
                // Isso elimina qualquer possibilidade de sobreposicao com a frente da arma.
                #if UNITY_REVERSED_Z
                    output.positionCS.z -= 0.0003 * output.positionCS.w;
                #else
                    output.positionCS.z += 0.0003 * output.positionCS.w;
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;
                half3 col = _OutlineColor.rgb * pulse;
                float a = saturate(_OutlineColor.a * _Alpha);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
