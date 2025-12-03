#include "Common.h"

using namespace metal;

typedef struct
{
    matrix_float4x4 model;
    matrix_float4x4 view;
    matrix_float4x4 projection;
} ShadowFrameData;

typedef struct
{
    float3 position [[ attribute(0) ]];
    float3 tex_coord [[ attribute(4) ]];
} ShadowVertexIn;

typedef struct
{
    float4 position [[ position ]];
    float2 tex_coord;
} ShadowVertexOut;

vertex ShadowVertexOut shadow_vertex(ShadowVertexIn in [[ stage_in ]], constant ShadowFrameData& frameData [[ buffer(5) ]])
{
    ShadowVertexOut out;
    float4 world_position = frameData.model * float4(in.position, 1.0);
    out.position = frameData.projection * frameData.view * world_position;
    out.tex_coord = in.tex_coord.xy;
    return out;
}

fragment void shadow_fragment(ShadowVertexOut in [[ stage_in ]], texture2d<float> opacityMask [[ texture(0) ]])
{
    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear, address::repeat);
    float opacity = opacityMask.sample(linearSampler, in.tex_coord).r;
    if (opacity < 0.5)
    {
        discard_fragment();
    }
}
