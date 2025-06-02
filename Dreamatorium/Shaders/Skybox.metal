#include "Common.h"

using namespace metal;

struct SkyboxOut
{
    float4 position [[position]];
    float3 direction;
};

vertex SkyboxOut skybox_vs(uint vertexID [[ vertex_id ]], constant FrameData & frameData [[ buffer(0) ]])
{
    float3 ndcCube[24] = {
      float3(-1.0, 1.0, 1.0),
      float3(-1.0,-1.0, 1.0),
      float3(1.0, -1.0, 1.0),
      float3(1.0, 1.0, 1.0),
      float3(1.0, 1.0, 1.0),
      float3(1.0, -1.0, 1.0),
      float3(1.0, -1.0,-1.0),
      float3(1.0, 1.0,-1.0),
      float3(1.0, 1.0,-1.0),
      float3(1.0,-1.0,-1.0),
      float3(-1.0, -1.0,-1.0),
      float3(-1.0, 1.0,-1.0),
      float3(-1.0, 1.0,-1.0),
      float3(-1.0, -1.0,-1.0),
      float3(-1.0, -1.0, 1.0),
      float3(-1.0, 1.0, 1.0),
      float3(-1.0, 1.0,-1.0),
      float3(-1.0, 1.0, 1.0),
      float3( 1.0, 1.0, 1.0),
      float3( 1.0, 1.0,-1.0),
      float3(-1.0, -1.0, 1.0),
      float3(-1.0, -1.0,-1.0),
      float3(1.0, -1.0,-1.0),
      float3(1.0, -1.0, 1.0),
     };

    uint cubeIndices[36] = {
        0, 1, 2, 0, 2, 3,
        4, 5, 6, 4, 6, 7,
        8, 9, 10, 8, 10, 11,
        12, 13, 14, 12, 14, 15,
        16, 17, 18, 16, 18, 19,
        20, 21, 22, 20, 22, 23,
    };

    float4 positionVS = frameData.view_rotation_matrix * float4(ndcCube[cubeIndices[vertexID]], 0.0);
    float4 positionCS = frameData.projection_matrix * float4(positionVS.xyz, 1.0);

    positionCS.z = positionCS.w;

    SkyboxOut out;
    out.direction = normalize(positionVS.xyz);
    out.position = positionCS;

    return out;
}

fragment float4 skybox_frag(SkyboxOut in [[ stage_in ]], texture2d<float> skybox_texture [[ texture(0) ]])
{
    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear);

    float3 dir = normalize(in.direction);

    float2 uv;
    uv.x = atan2(dir.z, dir.x) / (2.0 * PI) + 0.5;
    uv.y = asin(clamp(dir.y, -1.0, 1.0)) / PI + 0.5;

    float4 color = skybox_texture.sample(linearSampler, uv);

    return float4(color);
}
