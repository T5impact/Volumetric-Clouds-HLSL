using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NoiseVisualizerPass : ScriptableRenderPass
{
    //ProfilingSampler m_ProfilingSampler = new ProfilingSampler("ColorBlit");
    Material m_Material;
    RTHandle m_CameraColorTarget;
    float m_Intensity;

    public NoiseVisualizerPass(Material material)
    {
        m_Material = material;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public void SetTarget(RTHandle colorHandle, float intensity)
    {
        m_CameraColorTarget = colorHandle;
        m_Intensity = intensity;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        //ConfigureTarget(m_CameraColorTarget);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.camera.cameraType != CameraType.Game)
            return;

        if (m_Material == null)
            return;

        RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

        CommandBuffer cmd = CommandBufferPool.Get();
        //using (new ProfilingScope(cmd, m_ProfilingSampler))
       //{
         Blitter.BlitCameraTexture(cmd, cameraTargetHandle, cameraTargetHandle, m_Material, 0);
        //}
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }
    /* ProfilingSampler m_ProfilingSampler = new ProfilingSampler("ColorBlit");

     private Material material;
     private RenderTextureDescriptor textureDescriptor;
     private RTHandle textureHandle;

     public NoiseVisualizerPass(Material materialIn)
     {
         material = materialIn;

         textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
     }

     public void SetTarget(RTHandle colorHandle)
     {
         textureHandle = colorHandle;
     }

     public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
     {
         //Set the cloud texture size to be the same as the camera target size.
         textureDescriptor.width = cameraTextureDescriptor.width;
         textureDescriptor.height = cameraTextureDescriptor.height;

         //Check if the descriptor has changed, and reallocate the RTHandle if necessary.
         RenderingUtils.ReAllocateIfNeeded(ref textureHandle, textureDescriptor);
     }

     public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
     {
         //Get a CommandBuffer from pool.
         *//*CommandBuffer cmd = CommandBufferPool.Get();

         RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

         //Blit visualizer material on camera opaque texture to temp texture
         //Blit(cmd, cameraTargetHandle, textureHandle, material);
         //Blit temp texture back to camera texture
         //Blit(cmd, textureHandle, cameraTargetHandle, material);
         //Blit(cmd, cameraTargetHandle, cameraTargetHandle, material);
         Blitter.BlitCameraTexture(cmd, cameraTargetHandle, cameraTargetHandle, material, 0);

         // Blit from the camera target to the temporary render texture,
         // using the first shader pass.
         //Blit(cmd, cameraTargetHandle, textureHandle, material, 0);
         // Blit from the temporary render texture to the camera target,
         // using the second shader pass.
         //Blit(cmd, textureHandle, cameraTargetHandle, material, 1);

         Debug.Log(cameraTargetHandle.referenceSize);

         //Execute the command buffer and release it back to the pool.
         context.ExecuteCommandBuffer(cmd);
         //cmd.Clear();
         CommandBufferPool.Release(cmd);*//*

         var cameraData = renderingData.cameraData;
         if (cameraData.camera.cameraType != CameraType.Game)
             return;

         if (material == null)
             return;

         CommandBuffer cmd = CommandBufferPool.Get();
         using (new ProfilingScope(cmd, m_ProfilingSampler))
         {
             //m_Material.SetFloat("_Intensity", m_Intensity);
             Blitter.BlitCameraTexture(cmd, textureHandle, textureHandle, material, 0);
         }
         context.ExecuteCommandBuffer(cmd);
         cmd.Clear();

         CommandBufferPool.Release(cmd);
     }

     public void Dispose()
     {
         *//*#if UNITY_EDITOR
         if (EditorApplication.isPlaying)
         {
             Object.Destroy(material);
         }
         else
         {
             Object.DestroyImmediate(material);
         }
         #else
             Object.Destroy(material);
         #endif*//*

         if (textureHandle != null) textureHandle.Release();
     }*/
}
