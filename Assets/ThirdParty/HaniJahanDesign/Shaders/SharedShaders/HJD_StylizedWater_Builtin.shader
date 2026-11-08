// Intersection foam requires DepthTextureMode.Depth on every camera that renders
// the water.
Shader "Hani Jahan Design/StylizedWater/Built-in"
{
    Properties
    {
        [Header(Color and Transparency)]
        _BaseColor ("Base Water Color", Color) = (0.0, 0.3, 1.0, 1.0)
        [Toggle(_WATER_SECOND_COLOR)] _EnableSecondColor ("Use Second Color", Float) = 0
        _ShallowColor ("Shallow Water Color", Color) = (0.0, 0.7, 1.0, 1.0)
        _SecondColorHeight ("Second Color Height", Range(-20, 20)) = 0
        _SecondColorSpread ("Second Color Spread", Range(0.01, 10)) = 1
        _TransparencyMultiplier ("Opacity", Range(0, 2)) = 1.0
        [Toggle(_WATER_FRESNEL)] _EnableFresnel ("Edge Highlight", Float) = 1
        _FresnelPower ("Edge Size", Range(0.01, 8)) = 0.2

        [Header(Surface Ripples)]
        [Toggle(_WATER_RIPPLES)] _EnableRipples ("Surface Ripples", Float) = 1
        [KeywordEnum(Voronoi, Texture)] _RippleSource ("Ripple Style", Float) = 1
        _RippleTex ("Ripple Texture", 2D) = "gray" {}
        _RippleColor ("Ripple Color", Color) = (1, 1, 1, 1)
        _RippleScale ("Ripple Scale", Range(0.001, 5)) = 0.1
        _RippleSpeed ("Ripple Speed", Range(-2, 2)) = 0.05
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.35
        _RippleSharpness ("Ripple Sharpness", Range(0.1, 30)) = 10

        [Header(Intersection Foam)]
        [Toggle(_WATER_FOAM)] _EnableFoam ("Intersection Foam", Float) = 1
        _DepthThreshold ("Foam Reach", Range(0.01, 10)) = 0.5
        _FoamIntensity ("Foam Strength", Range(0, 3)) = 1
        [Toggle(_WATER_SECOND_FOAM)] _EnableSecondFoam ("Add Second Foam", Float) = 0
        [KeywordEnum(Bands, Texture)] _SecondFoamSource ("Second Foam Style", Float) = 0
        _SecondFoamTex ("Second Foam Texture", 2D) = "white" {}
        _SecondFoamColor ("Second Foam Color", Color) = (1, 1, 1, 1)
        _SecondFoamScale ("Second Foam Scale", Range(0.001, 5)) = 0.5
        _SecondFoamSpeed ("Second Foam Speed", Range(-2, 2)) = 0.01
        _SecondFoamDepthThreshold ("Second Foam Reach", Range(0.01, 10)) = 0.5
        _SecondFoamIntensity ("Second Foam Strength", Range(0, 3)) = 1
        _SecondFoamLineCount ("Second Foam Line Count", Range(1, 12)) = 4
        _SecondFoamLineThickness ("Second Foam Line Thickness", Range(0.001, 0.5)) = 0.17
        _SecondFoamEdgeSoftness ("Second Foam Edge Softness", Range(0.001, 0.2)) = 0.018

        [Header(Vertex Waves)]
        [Toggle(_WATER_VERTEX_WAVES)] _EnableVertexWaves ("Vertex Waves", Float) = 1
        _WaveHeight ("Wave Height", Range(0, 2)) = 0.1
        _WaveScale ("Wave Scale", Range(0.01, 10)) = 1.0
        _WaveSpeed ("Wave Speed", Range(-5, 5)) = 1.0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _WATER_FOAM
            #pragma shader_feature_local _WATER_SECOND_FOAM
            #pragma shader_feature_local _WATER_VERTEX_WAVES
            #pragma shader_feature_local _WATER_FRESNEL
            #pragma shader_feature_local _WATER_RIPPLES
            #pragma shader_feature_local _WATER_SECOND_COLOR
            #pragma shader_feature_local _RIPPLESOURCE_VORONOI _RIPPLESOURCE_TEXTURE
            #pragma shader_feature_local _SECONDFOAMSOURCE_BANDS _SECONDFOAMSOURCE_TEXTURE

            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _RippleTex;
            sampler2D _SecondFoamTex;

            float3 HJDTransformObjectToWorld(float3 positionOS)
            {
                return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
            }

            float4 HJDTransformWorldToHClip(float3 positionWS)
            {
                return UnityWorldToClipPos(positionWS);
            }

            float3 HJDTransformObjectToWorldNormal(float3 normalOS)
            {
                return UnityObjectToWorldNormal(normalOS);
            }

            float3 HJDGetWorldSpaceViewDir(float3 positionWS)
            {
                return UnityWorldSpaceViewDir(positionWS);
            }

            float4 HJDComputeScreenPos(float4 positionCS)
            {
                return ComputeScreenPos(positionCS);
            }

            float HJDLinearEyeDepthFromWorld(float3 positionWS)
            {
                return -mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z;
            }

            float HJDSampleSceneDepth(float4 screenPos)
            {
                return SAMPLE_DEPTH_TEXTURE_PROJ(
                    _CameraDepthTexture,
                    UNITY_PROJ_COORD(screenPos));
            }

            float HJDLinearEyeDepth(float rawDepth)
            {
                return LinearEyeDepth(rawDepth);
            }

            float HJDObjectOriginY()
            {
                return unity_ObjectToWorld._m13;
            }

            float HJDSampleRippleTexture(float2 uv)
            {
                return tex2D(_RippleTex, uv).r;
            }

            float HJDSampleSecondFoamTexture(float2 uv)
            {
                return tex2D(_SecondFoamTex, uv).r;
            }

            #include "HJD_StylizedWater_Common.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "HaniJahanDesign.StylizedWaterShaderPack.HJDStylizedWaterShaderGUI"
}
