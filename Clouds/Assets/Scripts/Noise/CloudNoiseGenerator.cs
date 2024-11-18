using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudNoiseGenerator : MonoBehaviour
{
    const int computeThreadGroupSize = 8;
    public const string detailNoiseName = "DetailNoise";
    public const string shapeNoiseName = "ShapeNoise";

    public enum CloudNoiseType { Shape, Detail }
    public enum TextureChannel { R, G, B, A }

    public CloudsController controller;

    [Header("Editor Settings")]
    public CloudNoiseType activeTextureType;
    public TextureChannel activeChannel;

    //public bool autoUpdate;
    //public bool logComputeTime;

    [Header("Noise Settings")]
    public int shapeResolution = 132;
    public int detailResolution = 32;
    public WorleyNoiseSettings[] shapeSettings;
    public WorleyNoiseSettings[] detailSettings;
    [SerializeField] ComputeShader noiseCompute;
    [SerializeField] ComputeShader copyCompute;
    [SerializeField] Material noiseVisualizerMat;


    [Header("Visualizer Settings")]
    public bool visualizerEnabled = true;
    [Range(0f,1f)]
    public float viewLayer = 1;
    public float viewScale = 1;
    public float viewTiling = 1;
    public bool viewGrayScale = true;
    public bool viewAllChannels = false;

    NoiseVisualizerPass visualizerPass;

    List<ComputeBuffer> buffersToRelease;
    bool updateNoise;

    [HideInInspector]
    public RenderTexture shapeTexture;
    [HideInInspector]
    public RenderTexture detailTexture;

    public RenderTexture ActiveTexture
    {
        get
        {
            if (activeTextureType == CloudNoiseType.Shape)
                return shapeTexture;
            else
                return detailTexture;
        }
    }
    public WorleyNoiseSettings ActiveSettings
    {
        get
        {
            if (activeTextureType == CloudNoiseType.Shape)
                return shapeSettings[Mathf.Min(shapeSettings.Length, (int)activeChannel)];
            else
                return detailSettings[Mathf.Min(detailSettings.Length, (int)activeChannel)];
        }
    }
    public Vector4 ChannelMask
    {
        get
        {
            return new Vector4(
                activeChannel == TextureChannel.R ? 1 : 0,
                activeChannel == TextureChannel.G ? 1 : 0,
                activeChannel == TextureChannel.B ? 1 : 0,
                activeChannel == TextureChannel.A ? 1 : 0
                );
        }
    }

    Vector3[] points;

    // Start is called before the first frame update
    void Start()
    {
        UpdateNoise();
        controller.InitializeClouds();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateNoise()
    {
        CreateTexture(ref shapeTexture, shapeResolution, shapeNoiseName);
        CreateTexture(ref detailTexture, detailResolution, detailNoiseName);

        if(updateNoise && noiseCompute)
        {
            updateNoise = false;

            WorleyNoiseSettings activeSettings = ActiveSettings;

            buffersToRelease = new List<ComputeBuffer>();

            int activeTextureResolution = ActiveTexture.width;

            // Set values:
            noiseCompute.SetFloat("persistence", activeSettings.persistance);
            noiseCompute.SetFloat("lacunarity", activeSettings.lacunarity);
            noiseCompute.SetFloat("scale", activeSettings.scale);
            noiseCompute.SetFloat("grid_scale", 1);
            noiseCompute.SetVector("offset", activeSettings.offset);
            noiseCompute.SetInt("resolution", activeTextureResolution);
            noiseCompute.SetVector("channelMask", ChannelMask);

            // Set noise gen kernel data:
            //noiseCompute.SetTexture(0, "Result", shapeTexture);
            //var minMaxBuffer = CreateBuffer(new int[] { int.MaxValue, 0 }, sizeof(int), "minMax", 0);
            int kernel = 0;
            //if (activeChannel == TextureChannel.R && activeTextureType == CloudNoiseType.Shape) kernel = 1;
            UpdateWorleyNoise(activeSettings, kernel);
            noiseCompute.SetTexture(kernel, "Result", ActiveTexture);
            //var noiseValuesBuffer = CreateBuffer (activeNoiseValues, sizeof (float) * 4, "values");

            // Dispatch noise gen kernel
            int numThreadGroups = Mathf.CeilToInt(shapeResolution / (float)computeThreadGroupSize);
            noiseCompute.Dispatch(kernel, numThreadGroups, numThreadGroups, numThreadGroups);

            // Set normalization kernel data:
            //noiseCompute.SetBuffer(1, "minMax", minMaxBuffer);
            //noiseCompute.SetTexture(1, "Result", ActiveTexture);
            // Dispatch normalization kernel
            //noiseCompute.Dispatch(1, numThreadGroups, numThreadGroups, numThreadGroups);

            /*if (logComputeTime)
            {
                // Get minmax data just to force main thread to wait until compute shaders are finished.
                // This allows us to measure the execution time.
                var minMax = new int[2];
                minMaxBuffer.GetData(minMax);

                Debug.Log($"Noise Generation: {timer.ElapsedMilliseconds}ms");
            }*/

            // Release buffers
            foreach (var buffer in buffersToRelease)
            {
                buffer.Release();
            }
        }
    }

    void UpdateWorleyNoise(WorleyNoiseSettings settings, int kernel = 0)
    {
        var prng = new System.Random(settings.seed);
        float maxDist2 = CreateWorleyNoisePointsBuffer(prng, settings.octave2Divisions, "octave2Points", kernel);
        float maxDist3 = CreateWorleyNoisePointsBuffer(prng, settings.octave3Divisions, "octave3Points", kernel);
        float maxDist1 = CreateWorleyNoisePointsBuffer(prng, settings.octave1Divisions, "octave1Points", kernel);

        noiseCompute.SetInt("octave1Divisions", settings.octave1Divisions);
        noiseCompute.SetInt("octave2Divisions", settings.octave2Divisions);
        noiseCompute.SetInt("octave3Divisions", settings.octave3Divisions);
        noiseCompute.SetFloat("maxDist1", maxDist1);
        noiseCompute.SetFloat("maxDist2", maxDist2);
        noiseCompute.SetFloat("maxDist3", maxDist3);
        noiseCompute.SetBool("invertNoise", settings.invert);
        //noiseCompute.SetInt("tile", settings.tile);
    }

    float CreateWorleyNoisePointsBuffer(System.Random prng, int numCellsPerAxis, string bufferName, int kernel = 0)
    {
        points = new Vector3[numCellsPerAxis * numCellsPerAxis * numCellsPerAxis];
        float cellSize = 1f / numCellsPerAxis;

        for (int x = 0; x < numCellsPerAxis; x++)
        {
            for (int y = 0; y < numCellsPerAxis; y++)
            {
                for (int z = 0; z < numCellsPerAxis; z++)
                {
                    float randomX = (float)prng.NextDouble();
                    float randomY = (float)prng.NextDouble();
                    float randomZ = (float)prng.NextDouble();
                    Vector3 randomOffset = new Vector3(randomX, randomY, randomZ) * cellSize;
                    Vector3 cellCorner = new Vector3(x, y, z) * cellSize;

                    int index = x + numCellsPerAxis * (y + z * numCellsPerAxis);
                    points[index] = cellCorner + randomOffset;
                }
            }
        }

        CreateNoiseBuffer(points, sizeof(float) * 3, bufferName, kernel);
        return Mathf.Sqrt(cellSize * cellSize * 2);
    }

    ComputeBuffer CreateNoiseBuffer(System.Array data, int stride, string bufferName, int kernel = 0)
    {
        ComputeBuffer buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
        buffersToRelease.Add(buffer);
        buffer.SetData(data);
        noiseCompute.SetBuffer(kernel, bufferName, buffer);
        return buffer;
    }

    void CreateTexture(ref RenderTexture texture, int resolution, string name)
    {
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
        if (texture == null || !texture.IsCreated() || texture.width != resolution || texture.height != resolution || texture.volumeDepth != resolution || texture.graphicsFormat != format)
        {
            //Debug.Log ("Create tex: update noise: " + updateNoise);
            if (texture != null)
            {
                texture.Release();
            }
            texture = new RenderTexture(resolution, resolution, 0);
            texture.graphicsFormat = format;
            texture.volumeDepth = resolution;
            texture.enableRandomWrite = true;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture.name = name;

            texture.Create();
            Load(name, texture);
        }
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
    }

    #region Visualizer Blit
    public void InitializeVisualizer()
    {
        noiseVisualizerMat.SetFloat("_ViewScale", viewScale);
        noiseVisualizerMat.SetFloat("_ViewLayer", viewLayer);
        noiseVisualizerMat.SetFloat("_ViewTile", viewTiling);
        noiseVisualizerMat.SetInt("_ViewGrayScale", (viewGrayScale && !viewAllChannels) ? 1 : 0);
        noiseVisualizerMat.SetVector("_ChannelMask", viewAllChannels ? new Vector4(1, 1, 1, 1) : ChannelMask);

        if(shapeTexture != null)
        {
            //print("Has Render Tex: " + noiseVisualizerMat.HasTexture("_RendTex"));
            noiseVisualizerMat.SetTexture("_RendTex", ActiveTexture);
        }

        visualizerPass = new NoiseVisualizerPass(noiseVisualizerMat);
    }
    private void OnEnable()
    {
        InitializeVisualizer();
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
        if (visualizerEnabled && noiseVisualizerMat != null)
        {
            // Use the EnqueuePass method to inject a custom render pass
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(visualizerPass);
        }
    }
    #endregion

    public void Load(string saveName, RenderTexture target)
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        saveName = sceneName + "_" + saveName;
        Texture3D savedTex = (Texture3D)Resources.Load(saveName);
        if (savedTex != null && savedTex.width == target.width)
        {
            copyCompute.SetTexture(0, "Tex", savedTex);
            copyCompute.SetTexture(0, "RenderTex", target);
            int numThreadGroups = Mathf.CeilToInt(savedTex.width / 8f);
            copyCompute.Dispatch(0, numThreadGroups, numThreadGroups, numThreadGroups);
        }
    }

    public struct R16
    {
        byte a;
        byte b;
    }

    public void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string saveName)
    {
        /*string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        saveName = sceneName + "_" + saveName;
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<R16>(width * height * depth * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, 0, (_) =>
        {
            Texture3D output = new Texture3D(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
            output.SetPixelData(a, 0);
            output.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            AssetDatabase.CreateAsset(output, $"Assets/Resources/{saveName}.asset");
            AssetDatabase.SaveAssetIfDirty(output);
            a.Dispose();
            rt3D.Release();
        });*/
    }

    public void ManualUpdate()
    {
        updateNoise = true;
        UpdateNoise();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, Vector3.one);

        if (points != null)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawWireSphere(transform.position + points[i] - new Vector3(0.5f, 0.5f, 0.5f), 0.01f);
            }
        }
    }
}
