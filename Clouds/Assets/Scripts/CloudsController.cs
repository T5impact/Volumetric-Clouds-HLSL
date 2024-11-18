using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class CloudsController : MonoBehaviour
{
    [SerializeField] Transform cloudsContainer;
    [SerializeField] Transform mainLight;
    [SerializeField] CloudNoiseGenerator noiseGenerator;
    [SerializeField] Texture2D blueNoiseTex;
    [SerializeField] Material cloudsMat;

    [Header("Input Settings")]
    [SerializeField] Slider cloudDensity;
    [SerializeField] Slider cloudCoverage;
    [SerializeField] Slider cloudScale;
    [SerializeField] Slider cloudTypeInput;
    [SerializeField] Slider cloudSpeed;

    [Header("Container Settings")]
    [SerializeField] float atmosphereRadius = 100;
    [SerializeField] float planetRadius = 1000;

    [Header("Cloud Settings")]
    [SerializeField] Vector2 cloudsMinMax = new Vector2(10, 100);
    [SerializeField] float marchStepSize = 10;
    [SerializeField] Vector2Int minMaxMarches = new Vector2Int(5, 10);
    [SerializeField] Vector2 marchDensityThresholds = new Vector2(0f, 0.2f);
    [SerializeField] float lightMarchStepSize = 10;
    [SerializeField] Vector2Int minMaxLightMarches = new Vector2Int(5, 10);
    [SerializeField] [Range(0,1)] float coneSamplingScale = 0.5f;
    [SerializeField] float coneSamplingRadius = 3;
    [SerializeField] float densityOffset = 0;
    [SerializeField] float densityScale = 0.1f;
    [SerializeField] float rayOffsetStrength = .1f;
    [SerializeField] Vector3 cloudType = new Vector3(1,0,0);
    [Header("Shape Settings")]
    [SerializeField] Vector3 shapeScale = new Vector3(1, 1, 1);
    [SerializeField] Vector3 shapeOffset = new Vector3(0,0,0);
    [SerializeField] Vector4 shapeWeights = new Vector4(1f, 0.7f, 0.5f, 0.3f);
    [Header("Detail Settings")]
    [SerializeField] Vector3 detailScale = new Vector3(1,1,1);
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
    [Header("Time Settings")]
    [SerializeField] float timeScale = 1;
    [SerializeField] float baseSpeed = 1;
    [SerializeField] float detailSpeed = 1;

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

        if(Application.isPlaying || Application.isEditor)
        {
            timeScale = cloudSpeed.value;
            switch((int)cloudTypeInput.value)
            {
                case 1:
                    cloudType = new Vector3(1, 0, 0);
                    break;
                case 2:
                    cloudType = new Vector3(0, 1, 0);
                    break;
                case 3:
                    cloudType = new Vector3(0, 0, 1);
                    break;
            }
            shapeScale = Vector3.one * cloudScale.value;
            densityOffset = cloudCoverage.value;
            densityScale = cloudDensity.value;
        }

        cloudsMat.SetFloat("timeScale", timeScale);
        cloudsMat.SetFloat("baseSpeed", baseSpeed);
        cloudsMat.SetFloat("detailSpeed", detailSpeed);

        cloudsMat.SetVector("clouds_minMax", cloudsMinMax);
        cloudsMat.SetVector("cloudType", cloudType);

        cloudsMat.SetVector("boundsMin", cloudsContainer.position - cloudsContainer.localScale / 2);
        cloudsMat.SetVector("boundsMax", cloudsContainer.position + cloudsContainer.localScale / 2);
        cloudsMat.SetVector("shapeScale", shapeScale);
        cloudsMat.SetVector("shapeOffset", shapeOffset);
        cloudsMat.SetVector("shapeWeights", shapeWeights);
        cloudsMat.SetVector("detailScale", detailScale);
        cloudsMat.SetVector("detailOffset", detailOffset);
        cloudsMat.SetVector("detailWeights", detailWeights);
        cloudsMat.SetFloat("detailWeight", detailWeight);

        cloudsMat.SetFloat("atmosphereRadius", atmosphereRadius + planetRadius);
        cloudsMat.SetFloat("planetRadius", planetRadius);
        cloudsMat.SetVector("center", cloudsContainer.position);

        cloudsMat.SetFloat("densityOffset", densityOffset);
        cloudsMat.SetFloat("densityScale", densityScale);

        cloudsMat.SetFloat("lightAbsorbtionTowardLight", lightAbsorbtionTowardLight);
        cloudsMat.SetFloat("lightAbsorbtionThroughCloud", lightAbsorbtionThroughCloud);

        cloudsMat.SetFloat("rayOffsetStrength", Mathf.Max(0, rayOffsetStrength));

        cloudsMat.SetVector("phaseParameters", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

        cloudsMat.SetFloat("marchStepSize", marchStepSize);
        cloudsMat.SetVector("minMaxMarches", (Vector2)minMaxMarches);
        cloudsMat.SetVector("marchDensityThresholds", marchDensityThresholds);
        cloudsMat.SetFloat("lightMarchStepSize", lightMarchStepSize);
        cloudsMat.SetVector("minMaxLightMarches", (Vector2)minMaxLightMarches);

        cloudsMat.SetFloat("coneSamplingScale", coneSamplingScale);
        cloudsMat.SetFloat("coneSamplingRadius", coneSamplingRadius);

        cloudsMat.SetVector("lightPos", mainLight.position);

        cloudsMat.SetTexture("_BlueNoise", blueNoiseTex);

        if (noiseGenerator != null)
        {
            cloudsMat.SetTexture("_ShapeTexture", noiseGenerator.shapeTexture);
            cloudsMat.SetTexture("_DetailTexture", noiseGenerator.detailTexture);
        }

        if (cloudsBlitPass != null) 
            cloudsBlitPass.UpdateMaterial(cloudsMat);
        else
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
