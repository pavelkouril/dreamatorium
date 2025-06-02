using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class VertexDescriptors
{
    public MTLVertexDescriptor Basic;

    public VertexDescriptors()
    {
        Basic = new MTLVertexDescriptor();

        var position = Basic.Attributes.Object(0);
        position.Format = MTLVertexFormat.Float3;
        position.BufferIndex = 0;

        var normals = Basic.Attributes.Object(1);
        normals.Format = MTLVertexFormat.Float3;
        normals.Offset = 0;
        normals.BufferIndex = 1;

        var tangents = Basic.Attributes.Object(2);
        tangents.Format = MTLVertexFormat.Float3;
        tangents.Offset = 0;
        tangents.BufferIndex = 2;

        var bitangents = Basic.Attributes.Object(3);
        bitangents.Format = MTLVertexFormat.Float3;
        bitangents.Offset = 0;
        bitangents.BufferIndex = 3;

        var texcoord0 = Basic.Attributes.Object(4);
        texcoord0.Format = MTLVertexFormat.Float3;
        texcoord0.Offset = 0;
        texcoord0.BufferIndex = 4;

        var posBuffer = Basic.Layouts.Object(0);
        posBuffer.Stride = 12;

        var normalBuffer = Basic.Layouts.Object(1);
        normalBuffer.Stride = 12;

        var tangentBuffer = Basic.Layouts.Object(2);
        tangentBuffer.Stride = 12;

        var bitangentBuffer = Basic.Layouts.Object(3);
        bitangentBuffer.Stride = 12;

        var texCoordsBuffer = Basic.Layouts.Object(4);
        texCoordsBuffer.Stride = 12;
    }
}
