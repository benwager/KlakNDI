using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Klak.Ndi {

[ExecuteInEditMode]
public sealed partial class NdiReceiver : MonoBehaviour
{
    #region Internal objects

    List<Interop.VideoFrame> videoFrameQueue = new List<Interop.VideoFrame>();
    List<float[]> audioBufferQueue = new List<float[]>();

    Thread _receiveThread;
    object threadlock = new object();
    bool _exitThread = false;

    Interop.Recv _recv;
    FormatConverter _converter;
    MaterialPropertyBlock _override;

    void PrepareInternalObjects()
    {
        if (_recv == null) _recv = RecvHelper.TryCreateRecv(_ndiName);
        if (_converter == null) _converter = new FormatConverter(_resources);
        if (_override == null) _override = new MaterialPropertyBlock();
    }

    void ReleaseInternalObjects()
    {
        lock (threadlock)
        {
            _recv?.Dispose();
            _recv = null;
        }
        _converter?.Dispose();
        _converter = null;
    }

    #endregion

    #region Receiver implementation

    void TryReceiveFrame()
    {
        while (!_exitThread)
        {              
            Interop.CaptureFrame? captureFrameOrNull;
        
            lock (threadlock)
            {
                if (_recv == null) continue;
                captureFrameOrNull = RecvHelper.TryCaptureFrame(_recv);
            }

            if(captureFrameOrNull == null)
            {
                continue;
            }

            Interop.CaptureFrame captureFrame = captureFrameOrNull.GetValueOrDefault();

            switch (captureFrame.frameType)
            {
                case Interop.FrameType.Video:

                    // Add to Queue for processing on Main thread
                    // We cannot free up the frame here because we need the data
                    // IntPtr address to be valid when processing on main thread.
                    // So we have to make sure to free the videoframe after processing
                    lock (threadlock)
                    {
                        // We only need to keep a single frame at any point
                        // So we can free up any previous frames first
                        videoFrameQueue.ForEach(vf => _recv.FreeVideoFrame(vf));
                        videoFrameQueue.Clear();
                        videoFrameQueue.Add(captureFrame.videoFrame);
                    }
                    break;

                case Interop.FrameType.Audio:
                    
                    // Create audio buffer

                    // we're working in bytes, so take the size of a 32 bit sample (float) into account
                    int sizeInBytes = (int)captureFrame.audioFrame.NoSamples * (int)captureFrame.audioFrame.NoChannels * sizeof(float);

                    // Unity audio is interleaved so we need to convert from planar
                    Interop.AudioFrameInterleaved audioFrameInterleaved = new Interop.AudioFrameInterleaved
                    {
                        SampleRate = captureFrame.audioFrame.SampleRate,
                        NoChannels = captureFrame.audioFrame.NoChannels,
                        NoSamples = captureFrame.audioFrame.NoSamples,
                        TimeCode = captureFrame.audioFrame.Timecode
                    };

                    // we need a managed byte array for our buffer
                    byte[] audBuffer = new byte[sizeInBytes];

                    // pin the byte[] and get a GC handle to it
                    // doing it this way saves an expensive Marshal.Alloc/Marshal.Copy/Marshal.Free later
                    // the data will only be moved once, during the fast interleave step that is required anyway
                    GCHandle handle = GCHandle.Alloc(audBuffer, GCHandleType.Pinned);

                    // access it by an IntPtr and use it for our interleaved audio buffer
                    audioFrameInterleaved.Data = handle.AddrOfPinnedObject();

                    // Convert from float planar to float interleaved audio
                    _recv.UtilAudioToInterleaved(ref captureFrame.audioFrame, ref audioFrameInterleaved);

                    // release the pin on the byte[]
                    // never try to access p_data after the byte[] has been unpinned!
                    // that IntPtr will no longer be valid.
                    handle.Free();

                    // Add buffer to queue for processing in audio thread
                    lock (threadlock)
                    {
                        audioBufferQueue.Add(ConvertByteArrayToFloat(audBuffer));
                    }

                    // We can free up the audio frame because this already been processed
                    // Converted to interleaved array of floats
                    _recv.FreeAudioFrame(captureFrame.audioFrame);

                    break;
            }
        }
    }

    internal float[] ConvertByteArrayToFloat(byte[] bytes)
    {
        if (bytes.Length % 4 != 0) throw new ArgumentException();

        float[] floats = new float[bytes.Length / 4];
        for (int i = 0; i < floats.Length; i++)
        {
            floats[i] = BitConverter.ToSingle(bytes, i * 4);
        }

        return floats;
    }
    #endregion

    #region Component state controller

    internal void Restart() => ReleaseInternalObjects();

    #endregion

    #region MonoBehaviour implementation

    void OnDisable() => ReleaseInternalObjects();

    void Start()
    {
        // start up a thread to receive on
        _exitThread = false;
        _receiveThread = new Thread(TryReceiveFrame) { IsBackground = true, Name = "ndiThread" };
        _receiveThread.Start();
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (threadlock)
        {
            if (audioBufferQueue.Count > 0)
            {
                for (int i = 0; i < audioBufferQueue[0].Length; i++)
                {
                    data[i] = audioBufferQueue[0][i];
                }
                audioBufferQueue.RemoveAt(0);
            }
        }
    }

    void LateUpdate()
    {
        PrepareInternalObjects();

        // Do nothing if the recv object is not ready.
        if (_recv == null) return;
    
        RenderTexture rt = null;
        lock (threadlock)
        {
            // Handle VideoFrames                
            if (videoFrameQueue.Count > 0)
            {
                // Unlike audio, we don't need to worry about processing every frame
                // There should only be a single frame in the queue anyway, 
                // But still, we grab the latest from the queue and discard the rest
                var vf = videoFrameQueue[videoFrameQueue.Count - 1];

                // Pixel format conversion
                rt = _converter.Decode
                  (vf.Width, vf.Height,
                   Util.CheckAlpha(vf.FourCC), vf.Data);

                // Copy the metadata if any.
                if (vf.Metadata != IntPtr.Zero)
                    metadata = Marshal.PtrToStringAnsi(vf.Metadata);
                else
                    metadata = null;

                // Store the videoframe resolution
                if (resolution == null || resolution.x != vf.Width || resolution.y != vf.Height)
                    resolution = new Vector2(vf.Width, vf.Height);

                // Free the videoframe
                videoFrameQueue.ForEach(v => _recv.FreeVideoFrame(v));
                videoFrameQueue.Clear();
            }
        }

        if (rt == null) return;

        // Material property override
        if (_targetRenderer != null)
        {
            _targetRenderer.GetPropertyBlock(_override);
            _override.SetTexture(_targetMaterialProperty, rt);
            _targetRenderer.SetPropertyBlock(_override);
        }

        // External texture update
        if (_targetTexture != null)
            Graphics.Blit(rt, _targetTexture);
    }

    private void OnDestroy()
    {
        _exitThread = true;
    }

    #endregion
}

}
