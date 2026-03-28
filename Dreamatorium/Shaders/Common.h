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
    float4 camera_position;
    matrix_float4x4 view_matrix;
    matrix_float4x4 projection_matrix;
    matrix_float4x4 inverse_view_matrix;
    matrix_float4x4 inverse_projection_matrix;
    matrix_float4x4 view_projection_matrix;
    matrix_float4x4 inverse_view_projection_matrix;
    matrix_float4x4 view_rotation_matrix;
    matrix_float4x4 light_view_matrix;
    matrix_float4x4 light_projection_matrix;
    matrix_float4x4 light_view_projection_matrix;
};

static float4 clipSpaceToViewSpace(float2 tex_coord, float depth, matrix_float4x4 inverse_projection_matrix)
{
    float z_ndc = depth;
    float2 ndc = float2(tex_coord.x * 2.0 - 1.0, 1.0 - tex_coord.y * 2.0);
    float4 clip = float4(ndc, z_ndc, 1.0);
    float4 view_pos = inverse_projection_matrix * clip;
    view_pos /= view_pos.w;
    return view_pos;
}

static float4 clipSpaceToWorldSpace(float2 tex_coord, float depth, matrix_float4x4 inverse_projection_matrix, matrix_float4x4 inverse_view_matrix)
{
    float4 view_pos = clipSpaceToViewSpace(tex_coord, depth, inverse_projection_matrix);
    float4 world_pos = inverse_view_matrix * view_pos;
    return world_pos;
}

#pragma clang diagnostic pop
