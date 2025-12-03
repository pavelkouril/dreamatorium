#include "Lighting.h"

using namespace metal;

constant bool kDebugShadowSampling = false;

struct QuadSimpleOut
{
    float4 positionCS [[ position ]];
    float2 tex_coord;
};

typedef struct {
    vector_float2 position;
} FullScreenVertexInput;

vertex QuadSimpleOut quad_vs(constant FullScreenVertexInput * vertices [[ buffer(0) ]], uint vid [[ vertex_id ]])
{
    QuadSimpleOut out;
    out.positionCS = float4(vertices[vid].position, 0, 1);
    out.tex_coord.x = (vertices[vid].position.x * 0.5) + 0.5;
    out.tex_coord.y = (1 - vertices[vid].position.y) * 0.5;
    return out;
}

fragment float4 lighting_frag(QuadSimpleOut in [[ stage_in ]], constant FrameData & frameData [[ buffer(0) ]], constant LightingData & lightingData [[ buffer(1) ]], texture2d<float> gBufferA [[ texture(0) ]], texture2d<float> gBufferB [[ texture(1) ]], depth2d<float> gBufferDepth [[ texture(2) ]], depth2d<float> shadowMap [[ texture(3) ]])
{
    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear);

    float4 gbufferASample = float4(gBufferA.sample(linearSampler, in.tex_coord));
    float4 gbufferBSample = float4(gBufferB.sample(linearSampler, in.tex_coord));
    float depth = gBufferDepth.sample(linearSampler, in.tex_coord);

    float3 albedo = gbufferASample.rgb;
    float roughness = gbufferASample.a;
    float metallic = gbufferBSample.a;

   // Reconstruct world-space position
   float3 positionWS = clipSpaceToWorldSpace(in.tex_coord, depth, frameData.inverse_projection_matrix, frameData.inverse_view_matrix).xyz;

   float3 N = normalize(gbufferBSample.rgb * 2.0 - 1.0);
   float3 V = normalize(frameData.camera_position - positionWS);

    float4 lightClipPosition = frameData.light_projection_matrix * frameData.light_view_matrix * float4(positionWS, 1.0);
    float3 lightNdc = lightClipPosition.xyz / max(lightClipPosition.w, 1e-5);
    float2 shadowUv = float2(lightNdc.x * 0.5 + 0.5, (1.0 - lightNdc.y) * 0.5);
    float currentLightDepth = lightNdc.z;

    constexpr sampler shadowSampler(mip_filter::linear, mag_filter::linear, min_filter::linear, address::clamp_to_edge);

    float directionalShadow = 1.0;
    bool insideShadowMap = shadowUv.x > 0.0 && shadowUv.x < 1.0 && shadowUv.y > 0.0 && shadowUv.y < 1.0;
    bool insideShadowDepth = currentLightDepth > 0.0 && currentLightDepth < 1.0;
    float closestDepth = 1.0;
    if (insideShadowMap && insideShadowDepth)
    {
        closestDepth = shadowMap.sample(shadowSampler, shadowUv);
        float NdotL = max(dot(N, normalize(lightingData.direction)), 0.0);
        float bias = max(0.002 * (1.0 - NdotL), 0.0005);
        directionalShadow = (currentLightDepth - bias) > closestDepth ? 0.0 : 1.0;
    }

    float3 color = calculateLighting(albedo, metallic, roughness, positionWS, N, V, &lightingData, 1, directionalShadow);

    // Reinhard operator tone mapping + gamma correction
    color = color / (color + float3(1.0));
    color = pow(color, 1.0 / 2.2);

    return float4(color, 1.0);
}
