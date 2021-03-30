using IntPtr = System.IntPtr;

namespace Klak.Ndi {

static class RecvHelper
{
    public static Interop.Source? FindSource(string sourceName)
    {
        foreach (var source in SharedInstance.Find.CurrentSources)
            if (source.NdiName == sourceName) return source;
        return null;
    }

    public static unsafe Interop.Recv TryCreateRecv(string sourceName, Interop.ColorFormat colorFormat = Interop.ColorFormat.Fastest, Interop.Bandwidth bandwidth = Interop.Bandwidth.Highest)
    {
        var source = FindSource(sourceName);
        if (source == null) return null;

        var opt = new Interop.Recv.Settings
          { Source = (Interop.Source)source,
            ColorFormat = colorFormat,
            Bandwidth = bandwidth };

        return Interop.Recv.Create(opt);
    }

    public static Interop.VideoFrame? TryCaptureVideoFrame(Interop.Recv recv)
    {
        Interop.VideoFrame video;
        var type = recv.Capture(out video, IntPtr.Zero, IntPtr.Zero, 0);
        if (type != Interop.FrameType.Video) return null;
        return (Interop.VideoFrame?)video;
    }

    public static Interop.CaptureFrame TryCaptureFrame(Interop.Recv recv)
    {
        Interop.CaptureFrame captureFrame = new Interop.CaptureFrame();

        captureFrame.frameType = recv.Capture(
            out captureFrame.videoFrame,
            out captureFrame.audioFrame,
            out captureFrame.metadataFrame,
        0);

        return captureFrame;
    }
}

}
