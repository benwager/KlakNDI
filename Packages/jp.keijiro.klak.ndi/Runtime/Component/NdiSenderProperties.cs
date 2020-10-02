using UnityEngine;
using UnityEngine.Events;

namespace Klak.Ndi {

public enum CaptureMethod { GameView, Camera, Texture }

public sealed partial class NdiSender : MonoBehaviour
{
    #region NDI source settings

    [SerializeField] string _ndiName = "NDI Sender";

    public string ndiName
      { get => _ndiName;
        set { _ndiName = value; Restart(); } }

    [SerializeField] bool _enableAlpha = false;

    public bool enableAlpha
      { get => _enableAlpha;
        set => _enableAlpha = value;
        }

    [SerializeField] bool _enableVideoFrames = true;

    public bool enableVideoFrames
    { get => _enableVideoFrames;
      set => _enableVideoFrames = value;
    }

    [SerializeField] bool _enableAudioFrames = true;

    public bool enableAudioFrames
    { get => _enableAudioFrames;
      set => _enableAudioFrames = value;
    }

    [SerializeField] bool _enableMetadataFrames = true;

    public bool enableMetadataFrames
    { get => _enableMetadataFrames;
      set => _enableMetadataFrames = value;
    }

    #endregion

    #region Capture target settings

    [SerializeField] CaptureMethod _captureMethod = CaptureMethod.GameView;

    public CaptureMethod captureMethod
      { get => _captureMethod;
        set { _captureMethod = value; Restart(); } }

    [SerializeField] Camera _sourceCamera = null;

    public Camera sourceCamera
      { get => _sourceCamera;
        set { _sourceCamera = value; ResetState(); } }

    [SerializeField] Texture _sourceTexture = null;

    public Texture sourceTexture
      { get => _sourceTexture;
        set => _sourceTexture = value; }

    #endregion

    #region metadata events

    [SerializeField] UnityEvent<string> _onMetadataReceived = new UnityEvent<string>();

    public UnityEvent<string> onMetaDataReceived
    {
        get => _onMetadataReceived;
        set => _onMetadataReceived = value;
    }

    [SerializeField] UnityEvent _onVideoMetadataSent = new UnityEvent();

    public UnityEvent onVideoMetadataSent
    {
        get => _onVideoMetadataSent;
        set => _onVideoMetadataSent = value;
    }

    [SerializeField] UnityEvent _onAudioMetadataSent = new UnityEvent();

    public UnityEvent onAudioMetadataSent
    {
        get => _onAudioMetadataSent;
        set => _onAudioMetadataSent = value;
    }

    [SerializeField] UnityEvent _onMetadataSent = new UnityEvent();

    public UnityEvent onMetadataSent
    { 
        get => _onMetadataSent;
        set => _onMetadataSent = value;
    }

    #endregion

    #region Runtime property

    public string sendVideoFrameMetadata { get; set; }
       
    public string sendAudioFrameMetadata { get; set; }

    public string sendMetadataFrameData { get; set; }

    public Interop.Send internalSendObject => _send;

    #endregion

    #region Resources asset reference

    [SerializeField, HideInInspector] NdiResources _resources = null;

    public void SetResources(NdiResources resources)
      => _resources = resources;

    #endregion
}

}
