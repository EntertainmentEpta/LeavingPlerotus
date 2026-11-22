Shader "Custom/GolemGroundRune_URP"
{
    Properties
    {
        _MainTex ("Rune / Circle Texture", 2D) = "white" {}
        [HDR] _Color ("Main Color (HDR)", Color) = (0.85, 0.55, 1.6, 1.0)
        [HDR] _CoreColor ("Highlight Color (HDR)", Color) = (2.2, 2.0, 2.8, 1.0)
        _Intensity ("Intensity", Float) = 1.35
        _Alpha ("Global Alpha", Range(0, 1)) = 1.0
        _FillProgress ("Radial Fill Progress", Range(0, 1)) = 0.0
        _OuterRingWidth ("Outer Ring Width", Range(0.005, 0.15)) = 0.032
        _ShowProceduralRing ("Show Procedural Ring", Range(0, 1)) = 1.0
        _UseTexture ("Use Texture Weight", Range(0, 1)) = 1.0
        _RotationSpeed ("Rotation Speed", Float) = 0.6
        _DepthBias ("Camera Depth Bias", Range(0, 0.003)) = 0.0008
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
            "Queue" = "Transparent+15"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GolemGroundRunePass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -3, -3
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _CoreColor;
                float _Intensity;
                float _Alpha;
                float _FillProgress;
                float _OuterRingWidth;
                float _ShowProceduralRing;
                float _UseTexture;
                float _RotationSpeed;
                float _DepthBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);

                // Bias suave em direcao a camera para nunca clipar/z-fightar em pisos irregulares
                #if UNITY_REVERSED_Z
                    output.positionCS.z += _DepthBias * output.positionCS.w;
                #else
                    output.positionCS.z -= _DepthBias * output.positionCS.w;
                #endif

                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float dist = length(centered);
                if (dist > 1.0) discard;

                // Rotacao suave da textura runica/tech
                float angle = _Time.y * _RotationSpeed;
                float s = sin(angle);
                float c = cos(angle);
                float2 rotUV = float2(centered.x * c - centered.y * s, centered.x * s + centered.y * c) * 0.5 + 0.5;

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rotUV);
                float texMask = max(texSample.a, max(texSample.r, max(texSample.g, texSample.b))) * _UseTexture;
                float edgeClip = 1.0 - smoothstep(0.95, 1.0, dist);
                texMask *= edgeClip;

                // Anel externo preciso no limite exato do raio de stun
                float ringInner = 1.0 - _OuterRingWidth;
                float outerRing = smoothstep(ringInner - 0.025, ringInner, dist) * (1.0 - smoothstep(0.975, 1.0, dist));

                // Onda de preenchimento radial do centro ate a borda (_FillProgress 0..1)
                float fillEdge = smoothstep(_FillProgress + 0.015, _FillProgress - 0.02, dist) * step(0.005, _FillProgress);
                float fillFrontRing = smoothstep(_FillProgress - 0.06, _FillProgress, dist) * (1.0 - smoothstep(_FillProgress, _FillProgress + 0.02, dist)) * step(0.02, _FillProgress);
                float fillSoft = fillEdge * (0.14 + 0.24 * smoothstep(0.0, max(0.01, _FillProgress), dist));

                // Pulso sutil de urgencia perto de 100%
                float urgency = saturate((_FillProgress - 0.72) / 0.28);
                float pulse = 1.0 + urgency * (0.35 + 0.25 * sin(_Time.y * 28.0));

                float procLayer = (outerRing * 0.95 + fillSoft + fillFrontRing * 0.85) * _ShowProceduralRing;
                float combined = saturate(texMask * 0.85 + procLayer);

                float highlight = saturate(outerRing * 0.75 + fillFrontRing * 0.95 + texMask * urgency * 0.55);
                half3 finalCol = lerp(_Color.rgb, _CoreColor.rgb, highlight) * _Intensity * pulse;

                return half4(finalCol, saturate(combined * _Alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
