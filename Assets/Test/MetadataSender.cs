using Klak.Ndi;
using UnityEngine;

public class MetadataSender : MonoBehaviour
{
    [SerializeField]
    public string videoFrameMetaData;

    [SerializeField]
    public string audioFrameMetaData;
    
    [SerializeField]
    public string metadataFrameData;

    private NdiSender sender;

    private void Awake()
    {
        sender = GetComponent<NdiSender>();
    }

    public void Update()
    {
        if (sender == null) return;
        sender.sendVideoFrameMetadata = videoFrameMetaData;
        sender.sendAudioFrameMetadata = audioFrameMetaData;
        sender.sendMetadataFrameData = metadataFrameData;
    }

    public void ClearVideFrameMetadata()
    {
        videoFrameMetaData = null;
    }

    public void ClearAudioFrameMetadata()
    {
        audioFrameMetaData = null;
    }

    public void ClearMetadataFrameMetadata()
    {
        metadataFrameData = null;
    }
}
