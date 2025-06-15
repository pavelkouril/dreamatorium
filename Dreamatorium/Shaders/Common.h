#pragma once

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunused-function"

#include <metal_stdlib>
#include <simd/simd.h>

#define PI 3.14159265359

using namespace metal;

struct FrameData
{
    float4 projection_parameters;
    float3 camera_position;
    matrix_float4x4 view_matrix;
    matrix_float4x4 projection_matrix;
    matrix_float4x4 inverse_view_matrix;
    matrix_float4x4 inverse_projection_matrix;
};

#pragma clang diagnostic pop