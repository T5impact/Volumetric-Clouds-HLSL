Shader "Clouds/CloudNoiseVisualize"
{
    Properties
    {
        _RendSetTex("Render Texture", 3D) = "white"
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "RenderTexVisualize"

        HLSLPROGRAM
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // input structure (Attributes) and output strucutre (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #pragma vertex Vert
        #pragma fragment frag

        CBUFFER_START(UnityPerMaterial)
            float4 _RendSetTex_ST;
        CBUFFER_END

        TEXTURE3D(_RendSetTex);
        SAMPLER(sampler_RendSetTex);

        TEXTURE3D(_RendTex);
        SAMPLER(sampler_RendTex);

        TEXTURE2D_X(_CameraOpaqueTexture);
        SAMPLER(sampler_CameraOpaqueTexture);

        float _Intensity;
        float _ViewScale = 0.3;
        float _ViewLayer = 1;
        float _ViewTile = 1;
        int _ViewGrayScale = true;
        float4 _ChannelMask = float4(1, 1, 1, 1);

        half4 frag(Varyings input) : SV_Target
        {
            //_ViewScale = 1;
            //_ViewTile = 2;
            //_ViewLayer = 1;
            uint minDimension = min(_ScreenParams.x, _ScreenParams.y);
            uint width = input.texcoord.x * _ScreenParams.x;
            uint height = input.texcoord.y * _ScreenParams.y;

            if (width > minDimension * _ViewScale || height > minDimension * _ViewScale) discard;

            float2 uv = float2(width / (minDimension * _ViewScale) * _ViewTile, height / (minDimension * _ViewScale) * _ViewTile);

            float4 noise = _RendTex.SampleLevel(sampler_RendTex, float3(uv.xy, _ViewLayer), 0);

            float4 maskedNoise = noise * _ChannelMask;
            if (_ViewGrayScale == 1) {
                return dot(maskedNoise, 1);
            }
            else {
                return maskedNoise;
            }
            //float4 noise = SAMPLE_TEXTURE3D(_RendTex, sampler_RendTex, float3(uv.xy, _ViewLayer));
            //half4 noise = tex3D(_RendTex, uint3(input.texcoord.xy, _ViewLayer));
        }
        ENDHLSL
    }
    }
}
