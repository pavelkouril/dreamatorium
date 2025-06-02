#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct GBufferData
{
    vector_float4 albedo_roughness [[ color(0), raster_order_group(0) ]];
    vector_float4 normal_metalness [[ color(1), raster_order_group(0) ]];
    float depth [[ color(2), raster_order_group(0) ]];
};

typedef struct
{
    matrix_float4x4 model;
    matrix_float4x4 view;
    matrix_float4x4 projection;
    matrix_float3x3 normal_matrix;
} GBufferFrameData;

typedef struct
{
    float3 position [[ attribute(0) ]];
    float3 normal [[ attribute(1) ]];
    float3 tangent [[ attribute(2) ]];
    float3 bitangent [[ attribute(3) ]];
    float3 tex_coord [[ attribute(4) ]];
} DescriptorDefinedVertex;

typedef struct
{
    float4 position [[ position ]];
    float2 tex_coord;
    float3 tangent;
    float3 bitangent;
    float3 normal;
    float3 view_space_position;
} Vert2Frag;

vertex Vert2Frag gbuffer_vertex(DescriptorDefinedVertex in [[ stage_in ]], constant GBufferFrameData & frameData [[ buffer(5) ]])
{
    Vert2Frag out;

    float4 world_position = frameData.model * float4(in.position, 1.0);
    float4 view_space_position = frameData.view * world_position;
    float4 screen_position = frameData.projection * view_space_position;
    out.position = screen_position;
    out.view_space_position = view_space_position.xyz;

    out.tex_coord = in.tex_coord.xy;

    out.tangent = normalize(frameData.normal_matrix * in.tangent);
    out.bitangent = -normalize(frameData.normal_matrix * in.bitangent);
    out.normal = normalize(frameData.normal_matrix * in.normal);

    return out;
}


fragment GBufferData gbuffer_fragment(Vert2Frag in [[ stage_in ]], texture2d<float> albedoTex [[ texture(0) ]], texture2d<float> normalMap [[ texture(1) ]], texture2d<float> opacityMask [[ texture(2) ]], texture2d<float> roughnessMask [[ texture(3) ]], texture2d<float> metalnessMask [[ texture(4) ]])
{
    GBufferData gBuffer;

    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear, address::repeat);

    float opacity = float4(opacityMask.sample(linearSampler, in.tex_coord.xy)).r;

    if (opacity < 0.5)
    {
        discard_fragment();
    }

    float3 base_color_sample = float4(albedoTex.sample(linearSampler, in.tex_coord.xy)).rgb;
    float roughness = float4(roughnessMask.sample(linearSampler, in.tex_coord.xy)).r;
    float metalness = float4(metalnessMask.sample(linearSampler, in.tex_coord.xy)).r;

    gBuffer.albedo_roughness = float4(base_color_sample, roughness);
    gBuffer.normal_metalness = float4(in.normal, metalness);
    gBuffer.depth = in.view_space_position.z;

    return gBuffer;
}