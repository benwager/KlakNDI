using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Klak.Ndi.Interop {

public class NDILib : SafeHandleZeroOrMinusOneIsInvalid
{
    #region SafeHandle implementation

    NDILib() : base(true) {}

    protected override bool ReleaseHandle()
    {
        _Destroy();
        return true;
    }

    #endregion

    #region Public methods

    public static bool Initialize()
      => _Initialize();

    public static void Destroy()
        => _Destroy();

    #endregion

    #region Unmanaged interface

    [DllImport(Config.DllName, EntryPoint = "NDIlib_initialize")]
    static extern bool _Initialize();

    [DllImport(Config.DllName, EntryPoint = "NDIlib_destroy")]
    static extern void _Destroy();

    #endregion
    }

}
