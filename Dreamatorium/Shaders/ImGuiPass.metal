#include <metal_stdlib>
using namespace metal;

struct ImGuiVertex
{
    packed_float2 position;
    packed_float2 uv;
    uint color;
};

struct VSOut
{
    float4 position [[position]];
    float2 uv;
    float4 color;
};

vertex VSOut imgui_vs(
    uint vertexId [[vertex_id]],
    constant ImGuiVertex* vertices [[buffer(0)]],
    constant float4x4& projection [[buffer(1)]])
{
    ImGuiVertex v = vertices[vertexId];
    VSOut o;
    float2 position = float2(v.position);
    float2 uv = float2(v.uv);
    uint c = v.color;
    float4 color = float4(
        float((c >> 0) & 0xFF),
        float((c >> 8) & 0xFF),
        float((c >> 16) & 0xFF),
        float((c >> 24) & 0xFF)) / 255.0;

    o.position = projection * float4(position, 0.0, 1.0);
    o.uv = uv;
    o.color = color;
    return o;
}

fragment float4 imgui_fs(
    VSOut in [[stage_in]],
    texture2d<float> fontTexture [[texture(0)]],
    sampler fontSampler [[sampler(0)]])
{
    return in.color * fontTexture.sample(fontSampler, in.uv);
}
