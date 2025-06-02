using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public static class ShaderLibrary
{
    private static MTLLibrary _instance;

    public static MTLLibrary GetOrCreate(MTLDevice device)
    {
        if (_instance.NativePtr == nint.Zero)
        {
            var libraryUrl = "Shaders/Output.metallib";
            var libraryError = new NSError();
            _instance = device.NewLibrary(StringHelper.NSString(libraryUrl), ref libraryError);
            if (libraryError.NativePtr != nint.Zero)
            {
                Console.Error.WriteLine($"Loading MTLLibrary failed. Reason: {StringHelper.String(libraryError.LocalizedDescription)}");
            }
        }

        return _instance;
    }
}
