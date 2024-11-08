using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CustomEditor(typeof(CloudsController))]
public class CloudsControlEditor : Editor
{
    CloudsController controller;

    private void OnEnable()
    {
        controller = (CloudsController)target;

        controller.InitializeClouds();
        RenderPipelineManager.beginCameraRendering += controller.OnBeginCamera;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        /*if (GUILayout.Button("Update"))
        {
            controller.ManualUpdate();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        if (GUILayout.Button("Save"))
        {
            controller.SaveRT3DToTexture3DAsset(controller.shapeTexture, CloudNoiseGenerator.shapeNoiseName);
            controller.SaveRT3DToTexture3DAsset(controller.detailTexture, CloudNoiseGenerator.detailNoiseName);
        }*/

        controller.InitializeClouds();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= controller.OnBeginCamera;
    }
}
