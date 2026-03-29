#include "Common.h"
#include <metal_raytracing>

using namespace metal;
using namespace metal::raytracing;

struct LightingData
{
    float4 position;
    float4 direction;
    float4 colorIntensity;
    uint type;
    uint _pad0;
    uint _pad1;
    uint _pad2;
};

kernel void rt_shadow_cs(constant FrameData& frameData [[ buffer(0) ]],
                         constant LightingData& lightingData [[ buffer(1) ]],
                         raytracing::instance_acceleration_structure sceneAS [[ buffer(2) ]],
                         texture2d<float, access::sample> gBufferB [[ texture(0) ]],
                         depth2d<float, access::sample> gBufferDepth [[ texture(1) ]],
                         texture2d<float, access::write> shadowMask [[ texture(2) ]],
                         uint2 gid [[ thread_position_in_grid ]])
{
    if (gid.x >= shadowMask.get_width() || gid.y >= shadowMask.get_height())
    {
        return;
    }

    float2 uv = (float2(gid) + 0.5) / float2(shadowMask.get_width(), shadowMask.get_height());
    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear);

    float4 gbufferBSample = gBufferB.sample(linearSampler, uv);
    float depth = gBufferDepth.sample(linearSampler, uv);

    float3 N = normalize(gbufferBSample.rgb);

    if (depth <= 0.0 || depth >= 1.0)
    {
        shadowMask.write(float4(1.0), gid);
        return;
    }

    float3 positionWS = clipSpaceToWorldSpace(uv, depth, frameData.inverse_projection_matrix, frameData.inverse_view_matrix).xyz;
    float3 L = normalize(lightingData.direction.xyz);

    constexpr float minDistance = 0.01;
    constexpr float maxDistance = 10000.0;

    raytracing::ray shadowRay;
    shadowRay.origin = positionWS + N * minDistance;
    shadowRay.direction = L;
    shadowRay.min_distance = minDistance;
    shadowRay.max_distance = maxDistance;

    raytracing::intersector<raytracing::triangle_data, raytracing::instancing> rayIntersector;
    rayIntersector.assume_geometry_type(raytracing::geometry_type::triangle);

    raytracing::intersection_result<raytracing::triangle_data, raytracing::instancing> hit = rayIntersector.intersect(shadowRay, sceneAS, 0xFF);
    float visibility = (hit.type == raytracing::intersection_type::none) ? 1.0 : 0.0;

    shadowMask.write(float4(visibility), gid);
}
