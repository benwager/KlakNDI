using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using static Klak.Ndi.MetadataQueue;

namespace Klak.Ndi {

[ExecuteInEditMode]
public sealed partial class NdiSender : MonoBehaviour
{
    #region Internal objects

    int _width, _height;
    Interop.Send _send;
    FormatConverter _converter;
    MetadataQueue _metadataQueue = new MetadataQueue();
    System.Action<AsyncGPUReadbackRequest> _onReadback;

    // Audio
    int numSamples = 0;
    int numChannels = 0;
    float[] samples = new float[1];

    Interop.VideoFrame emptyVideoFrame;
    GCHandle bufferHandle1;
    GCHandle bufferHandle2;
    byte[] videoFrameBuffer1 = null;
    byte[] videoFrameBuffer2 = null;
    int ping = 0;

    void PrepareInternalObjects()
    {
        emptyVideoFrame = new Interop.VideoFrame
        {
            Width = 0,
            Height = 0,
            LineStride = 0,
            FourCC = _enableAlpha ?
              Interop.FourCC.UYVA : Interop.FourCC.UYVY,
            FrameFormat = Interop.FrameFormat.Progressive,
            Data = IntPtr.Zero,
            Metadata = IntPtr.Zero
        };

        if (_send == null)
            _send = _captureMethod == CaptureMethod.GameView ?
              SharedInstance.GameViewSend : Interop.Send.Create(_ndiName);
        if (_converter == null) _converter = new FormatConverter(_resources);
        if (_onReadback == null) _onReadback = OnReadback;
    }

    void ReleaseInternalObjects()
    {
        if (_send != null && !SharedInstance.IsGameViewSend(_send))
            _send.Dispose();
        _send = null;

        _converter?.Dispose();
        _converter = null;
    }

    #endregion

    #region Immediate capture methods

    ComputeBuffer CaptureImmediate()
    {
        PrepareInternalObjects();

        // Texture capture method
        // Simply convert the source texture and return it.
        if (_captureMethod == CaptureMethod.Texture)
        {
            if (_sourceTexture == null) return null;

            _width = _sourceTexture.width;
            _height = _sourceTexture.height;

            return _converter.Encode(_sourceTexture, _enableAlpha, true);
        }

        // Game View capture method
        // Capture the screen into a temporary RT, then convert it.
        if (_captureMethod == CaptureMethod.GameView)
        {
            _width = Screen.width;
            _height = Screen.height;

            var tempRT = RenderTexture.GetTemporary(_width, _height, 0);

            ScreenCapture.CaptureScreenshotIntoRenderTexture(tempRT);
            var converted = _converter.Encode(tempRT, _enableAlpha, false);

            RenderTexture.ReleaseTemporary(tempRT);
            return converted;
        }

        Debug.LogError("Wrong capture method.");
        return null;
    }

    // Capture coroutine: At the end of every frames, it captures the source
    // frame, convert it to the NDI frame format, then request GPU readback.
    System.Collections.IEnumerator ImmediateCaptureCoroutine()
    {
        for (var eof = new WaitForEndOfFrame(); true;)
        {
            yield return eof;

            var converted = CaptureImmediate();
            if (converted == null) continue;

            AsyncGPUReadback.Request(converted, _onReadback);
            _metadataQueue.Enqueue(sendVideoFrameMetadata);
        }
    }

    #endregion

    #region SRP camera capture callback

    void OnCameraCapture(RenderTargetIdentifier source, CommandBuffer cb)
    {
        // NOTE: In some corner cases, this callback is called after object
        // destruction. To avoid these cases, we check the _attachedCamera
        // value and return if it's null. See ResetState() for details.
        if (_attachedCamera == null) return;

        PrepareInternalObjects();

        _width = _sourceCamera.pixelWidth;
        _height = _sourceCamera.pixelHeight;

        // Pixel format conversion
        var converted = _converter.Encode
          (cb, source, _width, _height, _enableAlpha, true);

        // GPU readback request
        cb.RequestAsyncReadback(converted, _onReadback);
        _metadataQueue.Enqueue(sendVideoFrameMetadata);
    }

    #endregion

    #region GPU readback completion callback

    unsafe void OnReadback(AsyncGPUReadbackRequest request)
    {
        // Metadata retrieval
        using (var metadata = _metadataQueue.Dequeue())
        {
            // Ignore errors.
            if (request.hasError) return;

            // Ignore it if the NDI object has been already disposed.
            if (_send == null || _send.IsInvalid || _send.IsClosed || !_enableVideoFrames) return;

            // Pixel format (depending on alpha mode)
            var fourcc = _enableAlpha ?
              Interop.FourCC.UYVA : Interop.FourCC.UYVY;

            // Readback data retrieval
            var data = request.GetData<byte>();
        
            // NDI SDK Documentation p.21 re: send_video_v2_async
            //
            // If you call this and then free the pointer, your application will
            // most likely crash in an NDI thread because the SDK is still using the video frame
            // that was passed to the call.
            // One possible solution is to ping pong between two buffers on 
            // alternating calls to NDIlib_send_send_video_v2_async

            if (data.Length <= 0) return;

            if (videoFrameBuffer1 == null || videoFrameBuffer1.Length <=0)
            {
                videoFrameBuffer1 = new byte[data.Length];
                bufferHandle1 = GCHandle.Alloc(videoFrameBuffer1, GCHandleType.Pinned);
            }

            if (videoFrameBuffer2 == null || videoFrameBuffer2.Length <= 0)
            {
                videoFrameBuffer2 = new byte[data.Length];
                bufferHandle2 = GCHandle.Alloc(videoFrameBuffer2, GCHandleType.Pinned);
            }

            // Handle frame size change
            if (videoFrameBuffer1.Length != data.Length)
            {
                _send.SendVideoAsync(emptyVideoFrame);
                bufferHandle1.Free();
                videoFrameBuffer1 = new byte[data.Length];
                bufferHandle1 = GCHandle.Alloc(videoFrameBuffer1, GCHandleType.Pinned);
            }
            if (videoFrameBuffer2.Length != data.Length)
            {
                _send.SendVideoAsync(emptyVideoFrame);
                bufferHandle2.Free();
                videoFrameBuffer2 = new byte[data.Length];
                bufferHandle2 = GCHandle.Alloc(videoFrameBuffer2, GCHandleType.Pinned);
            }

            // Ping pong handles
            var pdata = ping == 0 ? bufferHandle1.AddrOfPinnedObject() : bufferHandle2.AddrOfPinnedObject();
            data.CopyTo(ping == 0 ? videoFrameBuffer1 : videoFrameBuffer2);
            ping = ping == 0 ? 1 : 0;

            // Data size verification
            if (data.Length / sizeof(uint) !=
                Util.FrameDataCount(_width, _height, _enableAlpha)) return;

            // Frame data setup
            var frame = new Interop.VideoFrame
              { Width = _width, Height = _height, LineStride = _width * 2,
                FrameRateN = (int)frameRateND.x,
                FrameRateD = (int)frameRateND.y,
                FourCC = _enableAlpha ?
                  Interop.FourCC.UYVA : Interop.FourCC.UYVY,
                FrameFormat = Interop.FrameFormat.Progressive,
                Data = (System.IntPtr)pdata, Metadata = metadata };

            // Send via NDI
            _send.SendVideoAsync(frame);

            if(metadata != IntPtr.Zero)
            {
                _onVideoMetadataSent?.Invoke();
            }
        }
    }

    #endregion

    #region Component state controller

    Camera _attachedCamera;

    // Reset the component state without disposing the NDI send object.
    internal void ResetState(bool willBeActive)
    {
        // Disable the subcomponents.
        StopAllCoroutines();

        //
        // Remove the capture callback from the camera.
        //
        // NOTE: We're not able to remove the capture callback correcly when
        // the camera has been destroyed because we end up with getting a null
        // reference from _attachedCamera. To avoid causing issues in the
        // callback, we make sure that _attachedCamera has a null reference.
        //
        if (_attachedCamera != null)
        {
        #if KLAK_NDI_HAS_SRP
            CameraCaptureBridge.RemoveCaptureAction
              (_attachedCamera, OnCameraCapture);
        #endif
        }

        _attachedCamera = null;

        frameRateND = Util.FrameRateND(_frameRate);

        // The following blocks are to activate the subcomponents.
        // We can return here if willBeActive is false.
        if (!willBeActive) return;

        if (_captureMethod == CaptureMethod.Camera)
        {
            // Enable the camera capture callback.
            if (_sourceCamera != null)
            {
                _attachedCamera = _sourceCamera;
            #if KLAK_NDI_HAS_SRP
                CameraCaptureBridge.AddCaptureAction
                  (_attachedCamera, OnCameraCapture);
            #endif
            }
        }
        else
        {
            // Enable the immediate capture coroutine.
            StartCoroutine(ImmediateCaptureCoroutine());
        }
    }

    // Reset the component state and dispose the NDI send object.
    internal void Restart(bool willBeActivate)
    {
        if (_send != null && !_send.IsInvalid && !_send.IsClosed)
        {
            _send.SendVideoAsync(emptyVideoFrame);
        }
        if (bufferHandle1 != null && bufferHandle1.IsAllocated)
        {
            bufferHandle1.Free();
        }
        if (bufferHandle2 != null && bufferHandle2.IsAllocated)
        {
            bufferHandle2.Free();
        }

        videoFrameBuffer1 = videoFrameBuffer2 = null;

        ResetState(willBeActivate);
        ReleaseInternalObjects();
    }

    internal void ResetState() => ResetState(isActiveAndEnabled);
    internal void Restart() => Restart(isActiveAndEnabled);

    #endregion

    #region MonoBehaviour implementation

    void OnEnable() => ResetState();

    void OnDisable() => Restart(false);

    void OnDestroy() => Restart(false);
        
    public void SendAudio(float[] data, int channels)
    {
        if (data.Length == 0 || channels == 0 || !_enableAudioFrames) return;
        
        bool settingsChanged = false;
        int tempSamples = data.Length / channels;
                
        if (tempSamples != numSamples)
        {
            settingsChanged = true;
            numSamples = tempSamples;
        }

        if (channels != numChannels)
        {
            settingsChanged = true;
            numChannels = channels;
        }

        if (settingsChanged)
        {
            System.Array.Resize<float>(ref samples, numSamples * numChannels);
        }

        for (int ch = 0; ch < numChannels; ch++)
        {
            for (int i = 0; i < numSamples; i++)
            {
                samples[numSamples * ch + i] = data[i * numChannels];
            }
        }
        unsafe
        {
            fixed (float* p = samples)
            {
                var frame = new Interop.AudioFrame
                {
                    SampleRate = 48000,
                    NoChannels = channels,
                    NoSamples = numSamples,
                    ChannelStride = numSamples * sizeof(float),
                    Data = (System.IntPtr)p
                };

                if(!string.IsNullOrEmpty(sendAudioFrameMetadata))
                {
                    frame.Metadata = new DataEntry(sendAudioFrameMetadata);
                    _onAudioMetadataSent?.Invoke();
                }

                if (_send != null && !_send.IsClosed)
                {
                    _send.SendAudio(frame);
                }
            }
        }
    }

    private void LateUpdate()
    {
        // Check if the receiver has returned any metadataFrames
        if (_send != null && !_send.IsClosed)
        {
            Interop.MetadataFrame recvMetadataFrame = new Interop.MetadataFrame();
            while (_send.Capture(out recvMetadataFrame, 0) == Interop.FrameType.Metadata)
            {
                // Dispatch UnityEvent
                onMetaDataReceived?.Invoke(Util.Utf8ToString(recvMetadataFrame.Data));

                // Free the metadataFrame
                _send.FreeMetadata(ref recvMetadataFrame);
            }
        }

        if (!string.IsNullOrEmpty(sendMetadataFrameData) && _enableMetadataFrames)
        {
            // Send some metadata
            Interop.MetadataFrame metadataFrame = new Interop.MetadataFrame();
            int length;
            metadataFrame.Data = Util.StringToUtf8(sendMetadataFrameData, out length);
            metadataFrame.Length = length;
            if (_send != null && !_send.IsClosed)
            {
                _send.SendMetadata(metadataFrame);
            }
            Marshal.FreeHGlobal(metadataFrame.Data);
            sendMetadataFrameData = null;

            // Dispatch metaData sent event
            _onMetadataSent?.Invoke();
        }
    }
    #endregion
}

}
