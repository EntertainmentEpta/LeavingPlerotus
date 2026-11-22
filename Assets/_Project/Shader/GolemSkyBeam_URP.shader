Shader "Custom/GolemSkyBeam_URP"
{
    Properties
    {
        _MainTex ("Energy Streak Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Core Color (HDR)", Color) = (2.4, 2.1, 3.0, 1.0)
        [HDR] _OuterColor ("Outer Color (HDR)", Color) = (0.76, 0.45, 1.6, 1.0)
        _Intensity ("Intensity", Float) = 1.6
        _Alpha ("Global Alpha", Range(0, 1)) = 1.0
        _FresnelPower ("Edge Softness (Fresnel)", Range(0.3, 5.0)) = 1.6
        _ScrollSpeed ("Scroll Speed (Y)", Float) = 3.2
        _Twist ("Spiral Twist", Float) = 1.2
        _Tiling ("Streak Tiling (X,Y)", Vector) = (3.0, 2.0, 0, 0)
        _RevealBottom ("Reveal Bottom (0..1)", Range(0, 1)) = 0.0
        _RevealTop ("Reveal Top (0..1)", Range(0, 1)) = 1.0
        _EdgeSoftness ("Reveal Softness", Range(0.01, 0.3)) = 0.08
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
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GolemSkyBeamPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
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
                half4 _CoreColor;
                half4 _OuterColor;
                float _Intensity;
                float _Alpha;
                float _FresnelPower;
                float _ScrollSpeed;
                float _Twist;
                float4 _Tiling;
                float _RevealBottom;
                float _RevealTop;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 posWS      : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.posWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.posWS);

                // Fresnel suave nas bordas laterais do cilindro (evita contorno duro)
                float ndotv = saturate(abs(dot(normalWS, viewDirWS)));
                float edgeSoft = pow(ndotv, _FresnelPower);

                // Fluxo vertical limpo e atmosferico descendo do ceu para o impacto
                float t = _Time.y * _ScrollSpeed;
                float2 uvA = float2(input.uv.x * _Tiling.x + input.uv.y * _Twist, input.uv.y * _Tiling.y + t);
                float2 uvB = float2(input.uv.x * (_Tiling.x * 1.5) - input.uv.y * (_Twist * 0.4), input.uv.y * (_Tiling.y * 0.85) + t * 1.35);

                half texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA).r;
                half texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).r;
                float shimmer = 0.72 + 0.28 * (texA * 0.6 + texB * 0.4);

                // Fade natural no topo (ceu) e flare suave na base
                float topFade = 1.0 - smoothstep(0.68, 1.0, input.uv.y);
                float baseBoost = 1.0 + (1.0 - smoothstep(0.0, 0.12, input.uv.y)) * 0.55;

                // Reveal animado para o feixe rasgar de cima para baixo
                float revBot = smoothstep(_RevealBottom - _EdgeSoftness, _RevealBottom + _EdgeSoftness, input.uv.y);
                float revTop = 1.0 - smoothstep(_RevealTop - _EdgeSoftness, _RevealTop + _EdgeSoftness, input.uv.y);
                float revealMask = revBot * revTop;

                // Gradiente limpo entre halo externo e nucleo branco central
                float coreMask = pow(ndotv, _FresnelPower * 2.4);
                half3 col = lerp(_OuterColor.rgb, _CoreColor.rgb, saturate(coreMask * 1.35));
                col *= _Intensity * shimmer * baseBoost;

                float alpha = saturate(edgeSoft * shimmer * topFade * revealMask * _Alpha);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
