using System.Runtime.InteropServices.Marshalling;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public unsafe class ObjectiveCClassBuilder(string name)
{
    private string? _superClass;

    private int _extraBytes;

    private readonly List<(Selector Selector, string Type, nint FunctionPointer)> _methods = new();

    public ObjectiveCClassBuilder SetSuperClass(string superClass)
    {
        _superClass = superClass;
        return this;
    }

    public ObjectiveCClassBuilder SetExtraBytes(int extraBytes)
    {
        _extraBytes = extraBytes;
        return this;
    }

    public ObjectiveCClassBuilder AddMethod(Selector selector, nint functionPointer, string type)
    {
        _methods.Add((selector, type, functionPointer));
        return this;
    }

    public nint Build()
    {
        byte* namePointer = Utf8StringMarshaller.ConvertToUnmanaged(name);

        nint classPairPointer = ObjectiveC.objc_allocateClassPair(_superClass != null ? new ObjectiveCClass(_superClass) : nint.Zero, (char*)namePointer, _extraBytes);

        if (classPairPointer == nint.Zero)
        {
            throw new Exception($"Failed to create ObjectiveC class {name}.");
        }

        foreach (var method in _methods)
        {
            byte* type = Utf8StringMarshaller.ConvertToUnmanaged(method.Type);
            ObjectiveC.class_addMethod(classPairPointer, method.Selector, method.FunctionPointer, (char*)type);
        }
        ObjectiveC.objc_registerClassPair(classPairPointer);

        return classPairPointer;
    }
}
