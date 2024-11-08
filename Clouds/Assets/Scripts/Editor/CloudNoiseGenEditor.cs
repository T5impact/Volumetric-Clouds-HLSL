using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CustomEditor(typeof(CloudNoiseGenerator))]
public class CloudNoiseGenEditor : Editor
{
    CloudNoiseGenerator generator;

    private void OnEnable()
    {
        generator = (CloudNoiseGenerator)target;

        generator.InitializeVisualizer();
        RenderPipelineManager.beginCameraRendering += generator.OnBeginCamera;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Update"))
        {
            generator.ManualUpdate();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        if (GUILayout.Button("Save"))
        {
            generator.SaveRT3DToTexture3DAsset(generator.shapeTexture, CloudNoiseGenerator.shapeNoiseName);
            generator.SaveRT3DToTexture3DAsset(generator.detailTexture, CloudNoiseGenerator.detailNoiseName);
        }

        generator.InitializeVisualizer();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= generator.OnBeginCamera;
    }
}
