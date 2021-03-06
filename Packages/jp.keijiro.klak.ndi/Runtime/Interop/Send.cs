using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Klak.Ndi.Interop {

public class Send : SafeHandleZeroOrMinusOneIsInvalid
{
    #region SafeHandle implementation

    Send() : base(true) {}

    protected override bool ReleaseHandle()
    {
        _Destroy(handle);
        return true;
    }

    #endregion

    #region Public methods

    public static Send Create(string name)
    {
        var cname = Marshal.StringToHGlobalAnsi(name);
        var settings = new Settings { NdiName = cname };
        var ptr = _Create(settings);
        Marshal.FreeHGlobal(cname);
        return ptr;
    }

    public void SendVideoAsync(in VideoFrame data)
      => _SendVideoAsync(this, data);
   
    public bool SetTally(out Tally tally, uint timeout)
      => _SetTally(this, out tally, timeout);

    public void SendAudio(in AudioFrame data)
      => _SendAudioV3(this, data);

    public void SendMetadata(in MetadataFrame data)
      => _SendMetadata(this, data);

    public FrameType Capture(out MetadataFrame data, uint timeout)
      => _Capture(this, out data, timeout);

    public void FreeMetadata(ref MetadataFrame data)
      => _FreeMetadata(this, ref data);

    #endregion

    #region Unmanaged interface

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct Settings 
    {
        public IntPtr NdiName;
        public IntPtr Groups;
        [MarshalAsAttribute(UnmanagedType.U1)] public bool ClockVideo;
        [MarshalAsAttribute(UnmanagedType.U1)] public bool ClockAudio;
    }

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_create")]
    static extern Send _Create(in Settings settings);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_destroy")]
    static extern void _Destroy(IntPtr send);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_send_video_async_v2")]
    static extern void _SendVideoAsync(Send send, in VideoFrame data);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_get_tally")]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool _SetTally(Send send, out Tally tally, uint timeout);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_send_audio_v2")]
    static extern void _SendAudio(Send send, in AudioFrame data);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_send_audio_v3")]
    static extern void _SendAudioV3(Send send, in AudioFrame data);
        
    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_send_metadata")]
    static extern void _SendMetadata(Send send, in MetadataFrame data);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_capture")]
    static extern FrameType _Capture(Send send, out MetadataFrame data, uint timeout);

    [DllImport(Config.DllName, EntryPoint = "NDIlib_send_free_metadata")]
    static extern void _FreeMetadata(Send send, ref MetadataFrame data);

    #endregion
    }

}
