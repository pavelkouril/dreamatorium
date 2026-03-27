#include <metal_stdlib>

using namespace metal;

struct QuadSimpleOut
{
    float4 positionCS [[ position ]];
    float2 tex_coord;
};

struct DebugPresentData
{
    uint channelMode;
    uint _pad0;
    uint _pad1;
    uint _pad2;
};

fragment float4 debug_present_frag(QuadSimpleOut in [[ stage_in ]], constant DebugPresentData & debugData [[ buffer(0) ]], texture2d<float> sourceTexture [[ texture(0) ]])
{
    constexpr sampler linearSampler(mip_filter::linear, mag_filter::linear, min_filter::linear);
    float4 sampleValue = sourceTexture.sample(linearSampler, in.tex_coord);

    if (debugData.channelMode == 1)
    {
        return float4(sampleValue.aaa, 1.0);
    }

    return float4(sampleValue.rgb, 1.0);
}
