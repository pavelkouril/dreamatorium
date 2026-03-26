using System.Runtime.InteropServices;
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
        nint namePointer = Marshal.StringToHGlobalAnsi(name);
        try
        {
            nint classPairPointer = ObjectiveC.objc_allocateClassPair(_superClass != null ? new ObjectiveCClass(_superClass) : nint.Zero, (char*)namePointer, _extraBytes);

            // When this class is already registered, reuse it instead of failing.
            if (classPairPointer == nint.Zero)
            {
                nint existingClass = new ObjectiveCClass(name);
                if (existingClass == nint.Zero)
                {
                    throw new Exception($"Failed to create ObjectiveC class {name}.");
                }

                return existingClass;
            }

            foreach (var method in _methods)
            {
                nint type = Marshal.StringToHGlobalAnsi(method.Type);
                try
                {
                    ObjectiveC.class_addMethod(classPairPointer, method.Selector, method.FunctionPointer, (char*)type);
                }
                finally
                {
                    Marshal.FreeHGlobal(type);
                }
            }

            ObjectiveC.objc_registerClassPair(classPairPointer);

            return classPairPointer;
        }
        finally
        {
            Marshal.FreeHGlobal(namePointer);
        }
    }
}
