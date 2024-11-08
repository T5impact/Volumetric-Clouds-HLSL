Shader "Clouds/VolumetricClouds"
{
    Properties
    {
       // _RendSetTex("Render Texture", 3D) = "white"
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
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #pragma vertex CloudVert
        #pragma fragment CloudFrag

        struct CloudVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 viewDir : TEXCOORD2;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        /*CBUFFER_START(UnityPerMaterial)
            float4 _RendSetTex_ST;
        CBUFFER_END*/

        TEXTURE2D(_BlueNoise);
        SAMPLER(sampler_BlueNoise);

        TEXTURE3D(_ShapeTexture);
        SAMPLER(sampler_ShapeTexture);

        TEXTURE3D(_DetailTexture);
        SAMPLER(sampler_DetailTexture);

        TEXTURE2D_X(_CameraOpaqueTexture);
        SAMPLER(sampler_CameraOpaqueTexture);

        float3 lightPos;
        float4 phaseParameters; //forward scattering, backward scattering, base brightness, phase factor
        float lightAbsorbtionTowardLight;
        float lightAbsorbtionThroughCloud;

        float rayOffsetStrength;

        float3 boundsMin;
        float3 boundsMax;

        float marchStepSize = 10;
        float2 minMaxMarches;
        float lightMarchStepSize = 10;
        float2 minMaxLightMarches;

        float densityOffset = 0;
        float densityScale = 0.1;

        float shapeScale = 1.0;
        float3 shapeOffset = float3(0, 0, 0);
        float4 shapeWeights = float4(1, 0.5, 0.3, 0.1);
        float detailScale = 1.0;
        float3 detailOffset = float3(0, 0, 0);
        float4 detailWeights = float4(1, 0.5, 0.3, 0.1);
        float detailWeight = 1;

        float2 squareUV(float2 uv) {
            float width = _ScreenParams.x;
            float height = _ScreenParams.y;
            //float minDim = min(width, height);
            float scale = 1000;
            float x = uv.x * width;
            float y = uv.y * height;
            return float2 (x / scale, y / scale);
        }

        // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
        float2 rayBoxIntersect(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
            // Adapted from: http://jcgt.org/published/0007/03/04/
            float3 t0 = (boundsMin - rayOrigin) * invRaydir;
            float3 t1 = (boundsMax - rayOrigin) * invRaydir;
            float3 tmin = min(t0, t1);
            float3 tmax = max(t0, t1);

            float dstA = max(max(tmin.x, tmin.y), tmin.z);
            float dstB = min(tmax.x, min(tmax.y, tmax.z));

            // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
            // dstA is dst to nearest intersection, dstB dst to far intersection

            // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
            // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

            // CASE 3: ray misses box (dstA > dstB)

            float dstToBox = max(0, dstA);
            float dstInsideBox = max(0, dstB - dstToBox);
            return float2(dstToBox, dstInsideBox);
        }

        float remap(float v, float minOld, float maxOld, float minNew, float maxNew) {
            return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
        }

        // Henyey-Greenstein
        float hg(float a, float g) {
            float g2 = g * g;
            return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
        }

        float phase(float a) {
            float blend = .5;
            float hgBlend = hg(a, phaseParameters.x) * (1 - blend) + hg(a, -phaseParameters.y) * blend;
            return phaseParameters.z + hgBlend * phaseParameters.w;
        }

        float powderEffect(float d) {
            return 1 - exp(-d * 2);
        }

        float sampleDensity(float3 position) {

            float3 size = boundsMax - boundsMin;

            // Calculate falloff at along x/z edges of the cloud container
            const float containerEdgeFadeDst = 50;
            float dstFromEdgeX = min(containerEdgeFadeDst, min(position.x - boundsMin.x, boundsMax.x - position.x));
            float dstFromEdgeZ = min(containerEdgeFadeDst, min(position.z - boundsMin.z, boundsMax.z - position.z));
            float edgeWeight = min(dstFromEdgeZ, dstFromEdgeX) / containerEdgeFadeDst;

            // Calculate height gradient from weather map
            //float2 weatherUV = (size.xz * .5 + (rayPos.xz-boundsCentre.xz)) / max(size.x,size.z);
            //float weatherMap = WeatherMap.SampleLevel(samplerWeatherMap, weatherUV, mipLevel).x;
            float gMin = .2;
            float gMax = .7;
            float heightPercent = (position.y - boundsMin.y) / size.y;
            float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1, gMax, 0, 1));
            heightGradient *= edgeWeight;

            float3 shapeSamplePos = position * shapeScale * 0.01 + shapeOffset * 0.01;
            float4 shapeDensitySample = _ShapeTexture.SampleLevel(sampler_ShapeTexture, shapeSamplePos, 0);

            float4 normalizedShapeWeights = shapeWeights / dot(shapeWeights, 1);
            float weightedShapeDensity = dot(shapeDensitySample, normalizedShapeWeights) * heightGradient;

            float shapeDensity = weightedShapeDensity + densityOffset * 0.1;

            if (shapeDensity > 0) {

                float3 detailSamplePos = position * detailScale * 0.01 + detailOffset * 0.01;
                float3 detailDensitySample = _DetailTexture.SampleLevel(sampler_DetailTexture, detailSamplePos, 0).xyz;

                float3 normalizedDetailWeights = detailWeights.xyz / dot(detailWeights.xyz, 1);
                float weightedDetailDensity = dot(detailDensitySample, normalizedDetailWeights);

                float shapeEdgeGradient = 1 - shapeDensity;
                float detailErodeWeight = shapeEdgeGradient * shapeEdgeGradient * shapeEdgeGradient;
                float cloudDensity = shapeDensity - (weightedDetailDensity) * detailErodeWeight * detailWeight;

                return cloudDensity * densityScale * 0.1;
            }

            return 0;
        }

        float lightMarch(float3 position) {
            float3 toLightDir = GetMainLight().direction;
            float2 rayIntersectInfo = rayBoxIntersect(boundsMin, boundsMax, position, 1 / toLightDir);
            float distThroughContainer = rayIntersectInfo.y;

            float distTravelled = 0;
            float maxDistance = distThroughContainer;

            float stepSize = max(0.1, lightMarchStepSize);

            if (maxDistance / stepSize > minMaxLightMarches.y) {
                stepSize = maxDistance / minMaxLightMarches.y;
            }
            else if (maxDistance / stepSize < minMaxLightMarches.x) {
                stepSize = maxDistance / minMaxLightMarches.x;
            }

            float totalDensity = 0;
            while (distTravelled < maxDistance) {
                float3 rayPos = position + toLightDir * distTravelled;
                totalDensity += max(0,sampleDensity(rayPos) * stepSize);
                distTravelled += stepSize;
            }

            float lightTransmittance = exp(-totalDensity * lightAbsorbtionTowardLight * powderEffect(totalDensity));

            //return lightTransmittance;
            //return darknessThreshold + lightTransmittance * (1 - darknessThreshold)
            return 0.07 + lightTransmittance * (1 - 0.07);
        }

        CloudVaryings CloudVert(Attributes input)
        {
            CloudVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);


            output.positionCS = pos;
            output.texcoord = uv;

            float4 posOS = float4(2.0 * uv.xy - 1, UNITY_NEAR_CLIP_VALUE, -1);
            float3 viewDir = mul(unity_CameraInvProjection, posOS).xyz;
            output.viewDir = mul(unity_CameraToWorld, float4(viewDir, 0)).xyz;

            //output.viewDir = normalize(positions.positionWS.xyz - GetCameraPositionWS());
            //output.viewDir = normalize(GetCameraPositionWS());
            //output.viewDir = GetWorldSpaceViewDir(positions.positionWS.xyz);
            //output.viewDir = normalize(positions.positionWS.xyz);
            //output.viewDir = normalize(pos.xyz);
            //output.viewDir = normalize(posOS.xyz);
            //output.viewDir = posOS.xyz;
            //output.viewDir = positions.positionWS.xyz;
            //output.viewDir = mul(UNITY_MATRIX_V, posOS).xyz;
            //output.viewDir = normalize(mul(UNITY_MATRIX_MVP, posOS).xyz - GetCameraPositionWS());
            //output.viewDir = GetWorldSpaceViewDir(mul(UNITY_MATRIX_MV, posOS).xyz);
            //output.viewDir = posOS.xyz;

            return output;
        }

        half4 CloudFrag(CloudVaryings input) : SV_Target
        {
            float3 rayOrigin = _WorldSpaceCameraPos;
            float viewLength = length(input.viewDir);
            float3 rayDir = input.viewDir / viewLength;

            #if UNITY_REVERSED_Z
                real nonLinDepth = SampleSceneDepth(input.texcoord);
            #else
                // Adjust z to match NDC for OpenGL
                real nonLinDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(input.texcoord));
            #endif

            //float depth = SampleSceneDepth(input.texcoord);
            float linearEyeDepth = LinearEyeDepth(nonLinDepth, _ZBufferParams) * viewLength;
            //float depth = LinearDepthToEyeDepth(nonLinDepth);
            //float depth = EyeDepthToLinearDepth(nonLinDepth);

            float randomOffset = _BlueNoise.SampleLevel(sampler_BlueNoise, squareUV(input.texcoord * 3), 0);
            randomOffset *= rayOffsetStrength;

            //return float4(linearEyeDepth, linearEyeDepth, linearEyeDepth, 1);

            //float cosAngle = dot(rayDir, lightPos);
            float cosAngle = dot(rayDir, GetMainLight().direction);
            float phaseVal = phase(cosAngle);

            float2 rayIntersectInfo = rayBoxIntersect(boundsMin, boundsMax, rayOrigin, 1/rayDir);
            float distToContainer = rayIntersectInfo.x;
            float distThroughContainer = rayIntersectInfo.y;

            float distTravelled = randomOffset;
            float maxDistance = min(linearEyeDepth - distToContainer, distThroughContainer);

            float stepSize = max(0.01, marchStepSize);
            if (floor(maxDistance / stepSize) > minMaxMarches.y) {
                stepSize = maxDistance / minMaxMarches.y;
            }
            else if (maxDistance / stepSize < minMaxMarches.x) {
                stepSize = maxDistance / minMaxMarches.x;
            }
            float3 startPos = rayOrigin + rayDir * distToContainer;

            float transmittance = 1;
            float3 lightEnergy = 0;

            float totalDensity = 0;
            while (distTravelled < maxDistance) {
                float3 rayPos = startPos + rayDir * distTravelled;
                float density = max(0, sampleDensity(rayPos)) * stepSize;
                if (density > 0) {
                    float lightTransmittance = lightMarch(rayPos);
                    lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
                    transmittance *= exp(-density * stepSize * lightAbsorbtionThroughCloud * powderEffect(density));

                    if (transmittance <= 0.01)
                        break;
                }
                distTravelled += stepSize;
            }
            //transmittance = exp(-totalDensity);
            /*if (linearEyeDepth < distToContainer) discard;

            if(distThroughContainer > 0) return float4(0, 0, 0, 1);*/

            float3 toLightDir = GetMainLight().direction;

            float3 background = _CameraOpaqueTexture.Sample(sampler_CameraOpaqueTexture, input.texcoord).xyz;
            float3 cloudColor = lightEnergy * GetMainLight().color;
            //return float4(distToContainer, distToContainer, distToContainer, 1);
            //return float4(distThroughContainer, distThroughContainer, distThroughContainer, 1);
            return float4(background * transmittance + cloudColor, 0);
            //return float4(toLightDir, 1);
        }
        ENDHLSL
    }
    }
}
