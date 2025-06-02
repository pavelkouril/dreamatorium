using System.Runtime.InteropServices;

namespace Dreamatorium.Platforms.macOS;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void ObjCMessageNintNintNintDelegate(nint id, nint cmd, nint notification);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate bool ObjCMessageNintNintNintReturnBoolDelegate(nint id, nint cmd, nint notification);
