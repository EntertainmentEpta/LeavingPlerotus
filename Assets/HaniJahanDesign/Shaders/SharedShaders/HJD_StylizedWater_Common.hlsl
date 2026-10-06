#ifndef HJD_STYLIZED_WATER_COMMON_INCLUDED
#define HJD_STYLIZED_WATER_COMMON_INCLUDED

// Pipeline adapters are supplied by the small Built-in and URP shader wrappers.
// Everything below this boundary is render-pipeline independent.

// Wave and color properties
float _WaveSpeed;
float _WaveScale;
float _WaveHeight;
half4 _BaseColor;
half4 _ShallowColor;
float _SecondColorHeight;
float _SecondColorSpread;
float _FresnelPower;
float _TransparencyMultiplier;

// Foam properties
float _DepthThreshold;
float _FoamIntensity;
half4 _SecondFoamColor;
float _SecondFoamScale;
float _SecondFoamSpeed;
float _SecondFoamDepthThreshold;
float _SecondFoamIntensity;
float _SecondFoamLineCount;
float _SecondFoamLineThickness;
float _SecondFoamEdgeSoftness;

// Ripple properties
half4 _RippleColor;
float _RippleScale;
float _RippleSpeed;
float _RippleStrength;
float _RippleSharpness;

float2 Hash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float2 CurveVoronoiUV(float2 uv)
{
    // Bend the sampling domain before building the cells so the Voronoi
    // borders read as hand-drawn, blobby foam instead of straight cracks.
    float time = _Time.y * _RippleSpeed;
    float2 warp;
    warp.x = sin((uv.y * 1.65) + time * 1.37) + sin((uv.x + uv.y) * 0.73 - time * 0.81);
    warp.y = cos((uv.x * 1.48) - time * 1.11) + sin((uv.x - uv.y) * 0.91 + time * 0.63);
    return uv + warp * 0.18;
}

float VoronoiRipple(float2 uv)
{
    // Build the ripple from softly warped Voronoi borders. Using actual
    // distances (not squared distances) plus a small per-cell oval bias
    // rounds the cells into connected bubble-like curves.
    uv = CurveVoronoiUV(uv + _Time.y * _RippleSpeed);

    float2 cell = floor(uv);
    float2 local = frac(uv);
    float nearestDist = 8.0;
    float secondNearestDist = 8.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y);
            float2 hash = Hash22(cell + offset);
            float2 feature = 0.5 + (hash - 0.5) * 0.62;
            float angle = hash.x * 6.2831853;
            float2 axis = float2(cos(angle), sin(angle));
            float2 delta = offset + feature - local;

            // Slightly stretch each cell around a stable random axis. The
            // result keeps large/small Voronoi islands, but with more
            // organic, curvy, overlapping-looking outlines.
            float alongAxis = dot(delta, axis);
            delta -= axis * alongAxis * 0.22;
            float dist = length(delta);

            if (dist < nearestDist)
            {
                secondNearestDist = nearestDist;
                nearestDist = dist;
            }
            else
            {
                secondNearestDist = min(secondNearestDist, dist);
            }
        }
    }

    float border = secondNearestDist - nearestDist;
    float curvedLine = 1.0 - smoothstep(0.018, 0.16, border);
    float softFill = 1.0 - smoothstep(0.08, 0.42, nearestDist);
    float ripple = max(curvedLine, softFill * 0.28);
    return pow(saturate(ripple), max(_RippleSharpness * 0.55, 0.0001));
}

float SampleRipple(float2 worldXZ)
{
    float2 rippleUV = worldXZ * _RippleScale;

    #if defined(_RIPPLESOURCE_TEXTURE)
        rippleUV += _Time.y * _RippleSpeed;
        return pow(saturate(HJDSampleRippleTexture(rippleUV)), _RippleSharpness);
    #else
        return VoronoiRipple(rippleUV);
    #endif
}

float SampleSecondFoam(float2 worldXZ)
{
    float safeScale = max(_SecondFoamScale, 0.0001);
    float foamTime = _Time.y * _SecondFoamSpeed;
    float2 foamUV = worldXZ * safeScale + float2(foamTime, foamTime * 0.25);

    // Use a single scrolling sample so texture-based foam has one clear
    // travel direction and does not look like two foam sheets sliding over
    // each other in opposite directions.
    return saturate(HJDSampleSecondFoamTexture(foamUV));
}

float BuildFoamGradient(float normalizedDepth)
{
    return 1.0 - smoothstep(0.0, 1.0, saturate(normalizedDepth));
}

float BuildHeightBiasedFoamStrength(float worldHeight)
{
    float foamStrength = saturate(_FoamIntensity);

    #if defined(_WATER_VERTEX_WAVES)
        // Remove foam from wave troughs before wave crests as Strength is
        // lowered. The derivative keeps the moving boundary anti-aliased,
        // while the explicit zero case still removes the entire effect.
        float waveRange = max(_WaveHeight * 2.0, 0.0001);
        float waterOriginHeight = HJDObjectOriginY();
        float normalizedHeight = saturate(
            (worldHeight - waterOriginHeight + _WaveHeight) / waveRange);
        float heightCutoff = 1.0 - foamStrength;
        float edgeSoftness = max(fwidth(normalizedHeight), 0.01);
        float heightMask = smoothstep(
            heightCutoff - edgeSoftness,
            heightCutoff + edgeSoftness,
            normalizedHeight);
        return foamStrength <= 0.0 ? 0.0 : (foamStrength >= 1.0 ? 1.0 : heightMask);
    #else
        // Without height variation, retain Strength's original uniform
        // intensity behavior instead of inventing an arbitrary cutoff.
        return foamStrength <= 0.0 ? 0.0 : 1.0;
    #endif
}

float BuildSecondFoamBands(float normalizedDepth)
{
    float edgeSoftness = max(fwidth(normalizedDepth), _SecondFoamEdgeSoftness);
    float bandMask = 0.0;
    int lineCount = clamp((int)round(_SecondFoamLineCount), 1, 12);

    [unroll]
    for (int lineIndex = 0; lineIndex < 12; lineIndex++)
    {
        if (lineIndex < lineCount)
        {
            float lineOrder = lineIndex + 1.0;
            float lineCenter = lineOrder / (lineCount + 1.0);
            float lineDistance = abs(normalizedDepth - lineCenter);
            float lineMask = 1.0 - smoothstep(
                _SecondFoamLineThickness,
                _SecondFoamLineThickness + edgeSoftness,
                lineDistance);
            float lineFade = 1.0 - ((float)lineIndex / max((float)lineCount, 1.0)) * 0.65;
            bandMask = max(bandMask, lineMask * lineFade);
        }
    }

    return bandMask * BuildFoamGradient(normalizedDepth);
}

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
};

struct v2f
{
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float3 viewDir : TEXCOORD2;
    float4 screenPos : TEXCOORD3;
};

v2f vert (appdata v)
{
    v2f o;
    float3 worldPos = HJDTransformObjectToWorld(v.vertex.xyz);

    // Layer a broad swell and a smaller cross-wave from the existing speed,
    // scale, and height controls. This gives the surface a less grid-like
    // motion without adding inspector parameters.
    #if defined(_WATER_VERTEX_WAVES)
        float safeWaveScale = max(_WaveScale, 0.0001);
        float time = _Time.y * _WaveSpeed;
        float primaryPhase = dot(worldPos.xz, float2(0.85, 0.35)) * safeWaveScale + time;
        float secondaryPhase = dot(worldPos.xz, float2(-0.25, 0.95)) * safeWaveScale * 1.7 - time * 1.35;
        float wave = sin(primaryPhase) * _WaveHeight * 0.65;
        wave += sin(secondaryPhase) * _WaveHeight * 0.35;
        worldPos.y += wave;
    #endif

    o.pos = HJDTransformWorldToHClip(worldPos);
    o.worldPos = worldPos;

    // Derive a simple animated normal from the same wave layers so lighting,
    // Fresnel, and ripple overlays follow the deformed surface.
    #if defined(_WATER_VERTEX_WAVES)
        float primarySlope = cos(primaryPhase) * _WaveHeight * safeWaveScale * 0.65;
        float secondarySlope = cos(secondaryPhase) * _WaveHeight * safeWaveScale * 1.7 * 0.35;
        float3 waveNormal;
        waveNormal.x = -(primarySlope * 0.85 + secondarySlope * -0.25);
        waveNormal.z = -(primarySlope * 0.35 + secondarySlope * 0.95);
        waveNormal.y = 1.0;
        o.normal = normalize(waveNormal);
    #else
        o.normal = HJDTransformObjectToWorldNormal(v.normal);
    #endif

    o.viewDir = HJDGetWorldSpaceViewDir(worldPos);

    // Pass screen position
    o.screenPos = HJDComputeScreenPos(o.pos);
    o.screenPos.z = HJDLinearEyeDepthFromWorld(worldPos);

    return o;
}

half4 frag (v2f i) : SV_Target
{
    half4 color = _BaseColor;
    float transparency = _TransparencyMultiplier;
    float fresnel = 0.0;

    // Fresnel effect
    #if defined(_WATER_FRESNEL)
        fresnel = pow(1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), max(_FresnelPower, 0.0001));
        transparency = saturate(lerp(_TransparencyMultiplier * 0.35, _TransparencyMultiplier, fresnel));
    #endif

    float depthDiff = 0.0;
    #if defined(_WATER_FOAM)
        // Sample the depth texture only for variants that need scene depth.
        float sceneDepth = HJDSampleSceneDepth(i.screenPos);
        float linearSceneDepth = HJDLinearEyeDepth(sceneDepth);
        float linearFragDepth = i.screenPos.z;

        // Only geometry in front of the water surface should contribute
        // to the foam band.
        depthDiff = max(0.0, linearSceneDepth - linearFragDepth);
    #endif

    // Blend the optional second color by world-space height. A smooth
    // transition avoids a visible hard line while keeping its position
    // independent from scene-depth fading and intersection foam.
    #if defined(_WATER_SECOND_COLOR)
        float halfSpread = max(_SecondColorSpread * 0.5, 0.0001);
        float secondColorBlend = smoothstep(
            _SecondColorHeight - halfSpread,
            _SecondColorHeight + halfSpread,
            i.worldPos.y);
        color = lerp(_BaseColor, _ShallowColor, secondColorBlend);
    #endif

    #if defined(_WATER_FRESNEL) && defined(_WATER_SECOND_COLOR)
        color.rgb = lerp(color.rgb, _ShallowColor.rgb, fresnel * 0.35);
    #endif

    color.a = saturate(transparency);

    #if defined(_WATER_RIPPLES)
        float ripple = SampleRipple(i.worldPos.xz) * _RippleStrength;
        color.rgb = lerp(color.rgb, _RippleColor.rgb, ripple * _RippleColor.a);
    #endif

    #if defined(_WATER_FOAM)
        float normalizedIntersection = saturate(depthDiff / max(_DepthThreshold, 0.0001));
        float foamStrength = BuildHeightBiasedFoamStrength(i.worldPos.y);
        float foam = saturate(
            BuildFoamGradient(normalizedIntersection) * _FoamIntensity * foamStrength);
        color.rgb = lerp(color.rgb, 1.0, foam);

        // Keep intersection foam from being tinted by the water color
        // through transparent blending.
        color.a = saturate(max(color.a, foam));

        #if defined(_WATER_SECOND_FOAM)
            float secondNormalizedIntersection = saturate(depthDiff / max(_SecondFoamDepthThreshold, 0.0001));
            float secondFoam = 0.0;

            #if defined(_SECONDFOAMSOURCE_TEXTURE)
                secondFoam = SampleSecondFoam(i.worldPos.xz) * BuildFoamGradient(secondNormalizedIntersection);
            #else
                secondFoam = BuildSecondFoamBands(secondNormalizedIntersection);
            #endif

            secondFoam = saturate(secondFoam * _SecondFoamIntensity);
            float secondFoamAlpha = secondFoam * _SecondFoamColor.a;
            color.rgb = lerp(color.rgb, _SecondFoamColor.rgb, secondFoamAlpha);
            color.a = saturate(max(color.a, secondFoamAlpha));
        #endif
    #endif

    return color;
}

#endif
