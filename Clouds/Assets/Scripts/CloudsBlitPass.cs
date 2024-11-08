using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CloudsBlitPass : ScriptableRenderPass
{
    private Material cloudsMaterial;
    private RenderTextureDescriptor textureDescriptor;
    private RTHandle textureHandle;

    public CloudsBlitPass(Material material)
    {
        cloudsMaterial = material;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
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
        CommandBuffer cmd = CommandBufferPool.Get();

        RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

        Blitter.BlitCameraTexture(cmd, cameraTargetHandle, cameraTargetHandle, cloudsMaterial, 0);
        // Blit from the camera target to the temporary render texture,
        // using the first shader pass.
        //Blit(cmd, cameraTargetHandle, textureHandle, material, 0);
        // Blit from the temporary render texture to the camera target,
        // using the second shader pass.
        //Blit(cmd, textureHandle, cameraTargetHandle, material, 1);
        //Debug.Log("Currently executing");

        //Execute the command buffer and release it back to the pool.
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

   /* public void Dispose()
    {
        #if UNITY_EDITOR
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
        #endif

        if (textureHandle != null) textureHandle.Release();
    }*/
}
