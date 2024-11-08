using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudsController : MonoBehaviour
{
    [SerializeField] Transform cloudsContainer;
    [SerializeField] Transform mainLight;
    [SerializeField] CloudNoiseGenerator noiseGenerator;
    [SerializeField] Texture2D blueNoiseTex;
    [SerializeField] Material cloudsMat;

    [Header("Cloud Settings")]
    [SerializeField] float marchStepSize = 10;
    [SerializeField] Vector2Int minMaxMarches = new Vector2Int(5, 10);
    [SerializeField] float lightMarchStepSize = 10;
    [SerializeField] Vector2Int minMaxLightMarches = new Vector2Int(5, 10);
    [SerializeField] float densityOffset = 0;
    [SerializeField] float densityScale = 0.1f;
    [SerializeField] float rayOffsetStrength = .1f;
    [Header("Shape Settings")]
    [SerializeField] float shapeScale = 1;
    [SerializeField] Vector3 shapeOffset = new Vector3(0,0,0);
    [SerializeField] Vector4 shapeWeights = new Vector4(1f, 0.7f, 0.5f, 0.3f);
    [Header("Detail Settings")]
    [SerializeField] float detailScale = 1;
    [SerializeField] Vector3 detailOffset = new Vector3(0, 0, 0);
    [SerializeField] Vector4 detailWeights = new Vector4(1f, 0.7f, 0.5f, 0.3f);
    [SerializeField] float detailWeight = 0.1f;
    [Header("Lighting Settings")]
    [SerializeField] float lightAbsorbtionTowardLight = 0.2f;
    [SerializeField] float lightAbsorbtionThroughCloud = 1;
    [SerializeField] [Range(0,1)] float forwardScattering;
    [SerializeField] [Range(0, 1)] float backScattering;
    [SerializeField] [Range(0, 1)] float baseBrightness;
    [SerializeField] [Range(0, 1)] float phaseFactor;

    CloudsBlitPass cloudsBlitPass;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InitializeClouds()
    {
        /*if (shapeTexture != null)
        {
            //print("Has Render Tex: " + noiseVisualizerMat.HasTexture("_RendTex"));
            noiseVisualizerMat.SetTexture("_RendTex", ActiveTexture);
        }*/
        cloudsMat.SetVector("boundsMin", cloudsContainer.position - cloudsContainer.localScale / 2);
        cloudsMat.SetVector("boundsMax", cloudsContainer.position + cloudsContainer.localScale / 2);
        cloudsMat.SetFloat("shapeScale", shapeScale);
        cloudsMat.SetVector("shapeOffset", shapeOffset);
        cloudsMat.SetVector("shapeWeights", shapeWeights);
        cloudsMat.SetFloat("detailScale", detailScale);
        cloudsMat.SetVector("detailOffset", detailOffset);
        cloudsMat.SetVector("detailWeights", detailWeights);
        cloudsMat.SetFloat("detailWeight", detailWeight);

        cloudsMat.SetFloat("densityOffset", densityOffset);
        cloudsMat.SetFloat("densityScale", densityScale);

        cloudsMat.SetFloat("lightAbsorbtionTowardLight", lightAbsorbtionTowardLight);
        cloudsMat.SetFloat("lightAbsorbtionThroughCloud", lightAbsorbtionThroughCloud);

        cloudsMat.SetFloat("rayOffsetStrength", rayOffsetStrength);

        cloudsMat.SetVector("phaseParameters", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

        cloudsMat.SetFloat("marchStepSize", marchStepSize);
        cloudsMat.SetVector("minMaxMarches", (Vector2)minMaxMarches);
        cloudsMat.SetFloat("lightMarchStepSize", lightMarchStepSize);
        cloudsMat.SetVector("minMaxLightMarches", (Vector2)minMaxLightMarches);

        cloudsMat.SetVector("lightPos", mainLight.position);

        cloudsMat.SetTexture("_BlueNoise", blueNoiseTex);

        if (noiseGenerator != null)
        {
            cloudsMat.SetTexture("_ShapeTexture", noiseGenerator.shapeTexture);
            cloudsMat.SetTexture("_DetailTexture", noiseGenerator.detailTexture);
        }

        cloudsBlitPass = new CloudsBlitPass(cloudsMat);
    }
    private void OnEnable()
    {
        InitializeClouds();
        // Subscribe the OnBeginCamera method to the beginCameraRendering event.
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        //visualizerPass.Dispose();
    }
    public void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cloudsMat != null)
        {
            // Use the EnqueuePass method to inject a custom render pass
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(cloudsBlitPass);
        }
    }
}
