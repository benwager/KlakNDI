using UnityEngine;

namespace Klak.Ndi {

[RequireComponent(typeof(NdiSender))]
[ExecuteInEditMode]
public sealed class NdiAudioSender : MonoBehaviour
{
    NdiSender ndiSender;

    private void Awake()
    {
        ndiSender = GetComponent<NdiSender>();
    }

    public void OnAudioFilterRead(float[] data, int channels)
    {
        if (data.Length == 0 || channels == 0) return;
        ndiSender?.SendAudio(data, channels);
    }
}
}
