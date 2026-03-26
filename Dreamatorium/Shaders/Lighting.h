#pragma once

#include "Common.h"

struct LightingData
{
    float3 position;
    float3 direction;
    // Color RGB, Intensity A
    float4 colorIntensity;
    // 0 - Directional, 1 - Point
    uint type;
};

using namespace metal;

static float distributionGGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    return a2 / (PI * denom * denom);
}

static float geometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    return NdotV / (NdotV * (1.0 - k) + k);
}

static float geometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx1 = geometrySchlickGGX(NdotV, roughness);
    float ggx2 = geometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

static float3 fresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

static float3 calculateLighting(float3 albedo, float metallic, float roughness, float3 positionWS, float3 N, float3 V, constant LightingData* lightingData, uint lightCount, float directionalShadow)
{
    float3 Lo = float3(0.0);

    float3 F0 = float3(0.04);
    F0 = mix(F0, albedo, metallic);

    float3 diffuse = albedo / PI;

    for (uint i = 0; i < lightCount; ++i)
    {
        float attenuation = 1.0;
        float3 L;

        if (lightingData[i].type == 0)
        {
            L = normalize(lightingData[i].direction);
        }
        else
        {
            L = lightingData[i].position - positionWS;
            float distance = distance = length(L);
            L = normalize(L);

            attenuation = 1.0 / (distance * distance);
        }

        float3 H = normalize(V + L);

        float NDF = distributionGGX(N, H, roughness);
        float G = geometrySmith(N, V, L, roughness);
        float3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

        float3 kD = (1.0 - F) * (1.0 - metallic);

        float NdotL = max(dot(N, L), 0.0);
        float3 specular = NDF * G * F / (4.0 * max(dot(N, V), 0.0) * NdotL + 1e-5);

        float3 radiance = lightingData[i].colorIntensity.rgb * lightingData[i].colorIntensity.a * attenuation;
        float shadowFactor = (lightingData[i].type == 0) ? directionalShadow : 1.0;
        Lo += (kD * diffuse + specular) * radiance * NdotL * shadowFactor;
    }

    float3 ambient = float3(0.03) * albedo;
    return ambient + Lo;
}
