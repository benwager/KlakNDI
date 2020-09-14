using UnityEngine;
using UnityEditor;

namespace Klak.Ndi.Editor {

[CanEditMultipleObjects]
[CustomEditor(typeof(NdiSender))]
sealed class NdiSenderEditor : UnityEditor.Editor
{
    SerializedProperty _ndiName;
    SerializedProperty _enableAlpha;
    SerializedProperty _captureMethod;
    SerializedProperty _sourceCamera;
    SerializedProperty _sourceTexture; 
    SerializedProperty _enableVideoFrames;
    SerializedProperty _enableAudioFrames;
    SerializedProperty _enableMetadataFrames;
    SerializedProperty _onMetadataReceived;

    static class Styles
    {
        public static Label NdiName = "NDI Name";
    }

    void OnEnable()
    {
        var finder = new PropertyFinder(serializedObject);
        _ndiName = finder["_ndiName"];
        _enableAlpha = finder["_enableAlpha"];
        _captureMethod = finder["_captureMethod"];
        _sourceCamera = finder["_sourceCamera"];
        _sourceTexture = finder["_sourceTexture"];
        _enableVideoFrames = finder["_enableVideoFrames"];
        _enableAudioFrames = finder["_enableAudioFrames"];
        _enableMetadataFrames = finder["_enableMetadataFrames"];
        _onMetadataReceived = finder["_onMetaDataReceived"];
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var restart = false;

        if (_captureMethod.hasMultipleDifferentValues ||
            _captureMethod.enumValueIndex != (int)CaptureMethod.GameView)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.DelayedTextField(_ndiName, Styles.NdiName);
            restart |= EditorGUI.EndChangeCheck();
        }

        EditorGUILayout.PropertyField(_enableAlpha);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_captureMethod);
        var reset = EditorGUI.EndChangeCheck();

        EditorGUI.indentLevel++;

        if (_captureMethod.hasMultipleDifferentValues ||
            _captureMethod.enumValueIndex == (int)CaptureMethod.Camera)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceCamera);
            reset |= EditorGUI.EndChangeCheck();
        }

        if (_captureMethod.hasMultipleDifferentValues ||
            _captureMethod.enumValueIndex == (int)CaptureMethod.Texture)
            EditorGUILayout.PropertyField(_sourceTexture);

        EditorGUI.indentLevel--;

        EditorGUILayout.PropertyField(_enableVideoFrames);
        EditorGUILayout.PropertyField(_enableAudioFrames);
        EditorGUILayout.PropertyField(_enableMetadataFrames);
      
        EditorGUILayout.PropertyField(_onMetadataReceived, true);

        serializedObject.ApplyModifiedProperties();

        // Restart or reset the sender on property changes.
        if (restart) foreach (NdiSender ns in targets) ns.Restart();
        if (reset) foreach (NdiSender ns in targets) ns.ResetState();
    }
}

}
