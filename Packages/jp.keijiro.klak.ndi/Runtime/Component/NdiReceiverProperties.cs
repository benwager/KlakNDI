using UnityEngine;

namespace Klak.Ndi {

public sealed partial class NdiReceiver : MonoBehaviour
{
    #region NDI source settings

    [SerializeField] string _ndiName = null;

    public string ndiName
      { get => _ndiName;
        set { _ndiName = value; Restart(); } }

    #endregion

    #region Target settings

    [SerializeField] RenderTexture _targetTexture = null;

    public RenderTexture targetTexture
      { get => _targetTexture;
        set => _targetTexture = value; }

    [SerializeField] Renderer _targetRenderer = null;

    public Renderer targetRenderer
      { get => _targetRenderer;
        set => _targetRenderer = value; }

    [SerializeField] string _targetMaterialProperty = null;

    public string targetMaterialProperty
      { get => _targetMaterialProperty;
        set => _targetMaterialProperty = value; }

    [SerializeField] public AudioSource _targetAudioSource = null;

    public AudioSource targetAudioSource
      { get => _targetAudioSource;
        set => _targetAudioSource = value; }

    #endregion

    #region metadata

    [SerializeField] public string _connectionAcknowledgement;
    
    public string connectionAcknowledgement 
      { get => _connectionAcknowledgement; 
        set => _connectionAcknowledgement = value; }

    #endregion

    #region Runtime property

    public RenderTexture texture => _converter?.LastDecoderOutput;

    public string recvVideoFrameMetadata { get; set; }

    public string sendMetadataFrameData { get; set; }

    public Interop.Recv internalRecvObject => _recv;

    public Vector2 resolution { get; set; }

    #endregion

    #region Resources asset reference

    [SerializeField, HideInInspector] NdiResources _resources = null;

    public void SetResources(NdiResources resources)
      => _resources = resources;

    #endregion
}

}
