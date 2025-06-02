using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering.Resources;

public class Mesh(string name, Material material)
{
    public MTLBuffer _vertexPositionsBuffer;
    public MTLBuffer _vertexNormalsBuffer;
    public MTLBuffer _vertexTangentsBuffer;
    public MTLBuffer _vertexBitangentsBuffer;
    public MTLBuffer _vertexTextureCoordinatesBuffer;

    public MTLBuffer _indexBuffer;

    public Material Material { get; private set; } = material;

    public string Name { get; private set; } = name;

    public static Mesh FromAssimpMesh(Assimp.Mesh mesh, Material material, MTLDevice device)
    {
        var rv = new Mesh(mesh.Name, material);

        var positionsDataSize = (ulong)(mesh.VertexCount * Marshal.SizeOf<Vector3>());
        var normalsDataSize = (ulong)(mesh.VertexCount * Marshal.SizeOf<Vector3>());
        var tangentsDataSize = (ulong)(mesh.VertexCount * Marshal.SizeOf<Vector3>());
        var bitangentsDataSize = (ulong)(mesh.VertexCount * Marshal.SizeOf<Vector3>());
        var textureCoordsDataSize = (ulong)(mesh.VertexCount * Marshal.SizeOf<Vector3>());
        var indices = mesh.GetUnsignedIndices().ToList();
        var indexBufferSize = (ulong)(indices.Count * Marshal.SizeOf<uint>());

        rv._vertexPositionsBuffer = device.NewBuffer(positionsDataSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._vertexPositionsBuffer.Label = StringHelper.NSString($"{mesh.Name}/Positions");
        rv._vertexNormalsBuffer = device.NewBuffer(normalsDataSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._vertexNormalsBuffer.Label = StringHelper.NSString($"{mesh.Name}/Normals");
        rv._vertexTangentsBuffer = device.NewBuffer(tangentsDataSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._vertexTangentsBuffer.Label = StringHelper.NSString($"{mesh.Name}/Tangents");
        rv._vertexBitangentsBuffer = device.NewBuffer(bitangentsDataSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._vertexBitangentsBuffer.Label = StringHelper.NSString($"{mesh.Name}/Bitangents");
        rv._vertexTextureCoordinatesBuffer = device.NewBuffer(textureCoordsDataSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._vertexTextureCoordinatesBuffer.Label = StringHelper.NSString($"{mesh.Name}/TexCoords");
        rv._indexBuffer = device.NewBuffer(indexBufferSize, MTLResourceOptions.ResourceStorageModeManaged);
        rv._indexBuffer.Label = StringHelper.NSString($"{mesh.Name}/IndexBuffer");

        LoadData(mesh.Vertices, rv._vertexPositionsBuffer);
        LoadData(mesh.Normals, rv._vertexNormalsBuffer);
        LoadData(mesh.Tangents, rv._vertexTangentsBuffer);
        LoadData(mesh.BiTangents, rv._vertexBitangentsBuffer);
        LoadData(mesh.TextureCoordinateChannels[0], rv._vertexTextureCoordinatesBuffer);
        LoadData(indices, rv._indexBuffer);

        return rv;

        void LoadData<T>(List<T> data, MTLBuffer buffer)
        {
            data.CopyToBuffer(buffer);
            buffer.DidModifyRange(new NSRange
            {
                location = 0,
                length = buffer.Length,
            });
        }
    }
}
