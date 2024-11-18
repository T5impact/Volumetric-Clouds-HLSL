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

        float timeScale;
        float baseSpeed;
        float detailSpeed;

        float2 clouds_minMax;
        float atmosphereRadius;
        float planetRadius;
        float3 center;

        float3 cloudType;

        float3 lightPos;
        float4 phaseParameters; //forward scattering, backward scattering, base brightness, phase factor
        float lightAbsorbtionTowardLight;
        float lightAbsorbtionThroughCloud;

        float rayOffsetStrength;

        float3 boundsMin;
        float3 boundsMax;

        float marchStepSize = 10;
        float2 minMaxMarches;
        float2 marchDensityThresholds;
        float lightMarchStepSize = 10;
        float2 minMaxLightMarches;
        float coneSamplingScale;
        float coneSamplingRadius;

        float densityOffset = 0;
        float densityScale = 0.1;

        float3 shapeScale = 1.0;
        float3 shapeOffset = float3(0, 0, 0);
        float4 shapeWeights = float4(1, 0.5, 0.3, 0.1);
        float3 detailScale = 1.0;
        float3 detailOffset = float3(0, 0, 0);
        float4 detailWeights = float4(1, 0.5, 0.3, 0.1);
        float detailWeight = 1;

        float3 getConeSample(float3 pos, float radius, float depth) {
            return float3(sin(dot(pos, pos)) * radius * 0.001, cos(dot(pos, pos)) * radius * 0.01, depth);
        }
        // source: https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
        float3x3 coneRotationMatrix(float3 coneDir, float3 lightDir) {
            float3 a = normalize(coneDir);
            float3 b = normalize(lightDir);
            return float3x3(
                dot(a, b), -length(cross(a, b)), 0.0,
                length(cross(a, b)), dot(a, b), 0.0,
                0.0, 0.0, 1.0
            );
        }
        float2 squareUV(float2 uv) {
            float width = _ScreenParams.x;
            float height = _ScreenParams.y;
            //float minDim = min(width, height);
            float scale = 1000;
            float x = uv.x * width;
            float y = uv.y * height;
            return float2 (x / scale, y / scale);
        }

        float getHeightFractionForPoint(float h, float2 cloudMinMax)
        {
            // Get global fractional position in cloud zone .
            float height_fraction = (h - cloudMinMax.x) / (cloudMinMax.y - cloudMinMax.x);
            return saturate(height_fraction);
        }

        //Ray sphere intersect math found online
        //Returns (dstToSphere, dstThroughtSphere). If ray misses sphere, will return 0
        float2 raySphereIntersect(float3 rayOrigin, float3 rayDir, float3 center, float radius) {

            float3 toCenter = rayOrigin - center;

            float a = 1;
            float b = 2 * dot(toCenter, rayDir);
            float c = dot(toCenter, toCenter) - radius * radius;
            float discriminant = b * b - 4 * a * c;

            if (discriminant > 0) {
                float s = sqrt(discriminant);

                float dstToSphereNear = max(0, (-b - s) / (2 * a));
                float dstToSphereFar = (-b + s) / (2 * a);

                if (dstToSphereFar >= 0) {
                    return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                }
            }

            return float2(0, 0);
        }
        float getRelativeHeight(float3 pos) {
            return length(pos - center) - planetRadius;
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
        float clampRemap(float v, in float minOld, in float maxOld, in float minNew, in float maxNew) {
            float clv =clamp(v, minOld, maxOld);
            return (clv - minOld) * (maxNew - minNew) / (maxOld - minOld) + minNew;
        }

        float cloudRemap(float h, float a, float b, float c) {
            return clampRemap(h, 0, a, 0, 1) * clampRemap(h, b, c, 1, 0);
        }
        float3 lerp3(float t) {
            float x = clamp(1 - t * 2, 0, 1);
            float z = clamp((t - 0.5) * 2, 0, 1);
            float y = 1 - x - z;
            return float3(x, y, z);
        }
        float heightGradient(float height_fraction, float h) {
            // calc cloud gradients
            float a = cloudRemap(h, 0.1, 0.2, 0.3);
            float b = cloudRemap(h, 0.2, 0.3, 0.5);
            float c = cloudRemap(h, .2, .7, .95);
            // calc weights
            //float3 weights = lerp3(h);
            float cloudGradient = a * cloudType.x + b * cloudType.y + c * cloudType.z;
            return cloudGradient * h;
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
            float3 uvw = (size * .5 + position) * 1/1000.0 * shapeScale / 2;

            //float height = getRelativeHeight(position);
            float height = position.y - boundsMin.y;

            float height_fraction = getHeightFractionForPoint(height, clouds_minMax);

            // Calculate falloff at along x/z edges of the cloud container
            const float containerEdgeFadeDst = 50;
            float dstFromEdgeX = min(containerEdgeFadeDst, min(position.x - boundsMin.x, boundsMax.x - position.x));
            float dstFromEdgeZ = min(containerEdgeFadeDst, min(position.z - boundsMin.z, boundsMax.z - position.z));
            float edgeWeight = min(dstFromEdgeZ, dstFromEdgeX) / containerEdgeFadeDst;

            // Calculate height gradient from weather map
            //float2 weatherUV = (size.xz * .5 + (rayPos.xz-boundsCentre.xz)) / max(size.x,size.z);
            //float weatherMap = WeatherMap.SampleLevel(samplerWeatherMap, weatherUV, mipLevel).x;
            /*float gMin = .2;
            float gMax = .7;
            float heightPercent = (position.y - boundsMin.y) / size.y;
            float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1, gMax, 0, 1));
            heightGradient *= edgeWeight;*/

            float time = _Time.x * timeScale;

            float3 shapeSamplePos = uvw * shapeScale * 0.01 + shapeOffset * 0.01 + float3(time, time * 0.1, time * 0.2) * baseSpeed;
            float4 shapeNoises = _ShapeTexture.SampleLevel(sampler_ShapeTexture, shapeSamplePos, 0);

            float4 shapeWeights_normalized = shapeWeights / dot(shapeWeights, 1);
            float shape_fbm = dot(shapeNoises, shapeWeights_normalized);// *heightGradient;

            shape_fbm += densityOffset * 0.1;

            float shape_cloud = remap(shapeNoises.r, -(1.0 - shape_fbm), 1.0, 0.0, 1.0) * edgeWeight;

            if (shape_cloud > 0) {

                float3 detailSamplePos = uvw * detailScale * 0.01 + detailOffset * 0.01 + float3(time, time * 0.4, time * 0.2) * detailSpeed;
                float3 detailNoises = _DetailTexture.SampleLevel(sampler_DetailTexture, detailSamplePos, 0).xyz;

                float3 detailWeights_normalized = detailWeights.xyz / dot(detailWeights.xyz, 1);
                float detail_fbm = dot(detailNoises, detailWeights_normalized);

                float detail_cloud = lerp(detail_fbm, 1.0 - detail_fbm, saturate(height_fraction - 10.0));
                detail_cloud *= detailWeight;

                float shapeEdgeGradient = 1 - shape_cloud;
                float detailErodeWeight = shapeEdgeGradient * shapeEdgeGradient;// *shapeEdgeGradient;
                //float cloudDensity = shape_cloud - (1 - weightedDetailDensity) * detailErodeWeight * detailWeight;

                float final_cloud = shape_cloud - (1 - detail_cloud) * detailErodeWeight;
               // float final_cloud = remap(shape_cloud, detail_cloud - 0.2, 1.0, 0.0, 1.0);

                //return final_cloud * densityScale * 0.1 * height_fraction;// *heightGradient(0, height / size.y);
                return final_cloud * densityScale * 0.1 * heightGradient(height_fraction, height / size.y) - height_fraction * cloudType.z * 0.2;
            }

            return 0;
        }

        float lightMarch(float3 position, in float coneDepths[6]) {
            float cosAngle = dot(normalize(GetViewForwardDir()), -normalize(GetMainLight().direction));

            float3 toLightDir = GetMainLight().direction;
            //float2 atmoIntersect = raySphereIntersect(position, toLightDir, center, atmosphereRadius);
            float2 rayIntersectInfo = rayBoxIntersect(boundsMin, boundsMax, position, 1 / toLightDir);
            float distThroughContainer = rayIntersectInfo.y;

            float3x3 coneRot = coneRotationMatrix(float3(0, 0, 1), -toLightDir);

            float distTravelled = 0;
            float maxDistance = distThroughContainer;

            float stepSize = max(0.1, lightMarchStepSize);

            /*if (maxDistance / stepSize > minMaxLightMarches.y) {
            }
            else if (maxDistance / stepSize < minMaxLightMarches.x) {
                stepSize = maxDistance / minMaxLightMarches.x;
            }*/
            stepSize = distThroughContainer / 6.0;

            float totalDensity = 0;
            while (distTravelled < maxDistance) {
                float3 rayPos = position + toLightDir * distTravelled;
                totalDensity += max(0,sampleDensity(rayPos) * stepSize);
                distTravelled += stepSize;
            }
            //for (uint i = 0; i < 6; i++)
            //{
            //    float depth = distThroughContainer * coneSamplingScale;
            //    //float3 rayPos = position + mul(coneRot, coneSamples[i]) * distThroughContainer / 3.0 * coneSamplingScale;
            //    //float3 rayPos = position + mul(coneRot, float3(.1 * i,.1 * i,0.1 * i)) * 100.0 * coneSamplingScale;
            //    float3 rayPos = position + mul(coneRot, getConeSample(position, coneSamplingRadius * coneDepths[i], coneDepths[i])) * depth;
            //    totalDensity += max(0, sampleDensity(rayPos)) * stepSize;
            //}

            float powder = powderEffect(totalDensity * .1);
            float lightTransmittance = exp(-totalDensity * lightAbsorbtionTowardLight) * lerp(1, powder, saturate(cosAngle) * (1 - totalDensity));

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

            //float2 atmoSphereIntersect = raySphereIntersect(rayOrigin, rayDir, center, atmosphereRadius);
            //float2 planSphereIntersect = raySphereIntersect(rayOrigin, rayDir, center, planetRadius);

            //float distToAtmo = atmoSphereIntersect.x;
            //float distThroughAtmo = planSphereIntersect.x > 0 ? min(atmoSphereIntersect.y, planSphereIntersect.x - atmoSphereIntersect.x) : atmoSphereIntersect.y;

            float2 rayIntersectInfo = rayBoxIntersect(boundsMin, boundsMax, rayOrigin, 1/rayDir);
            float distToContainer = rayIntersectInfo.x;
            float distThroughContainer = rayIntersectInfo.y;

            float distTravelled = randomOffset;
            float maxDistance = min(linearEyeDepth - distToContainer, distThroughContainer);

            float maxStepSize = maxDistance / minMaxMarches.x;
            float minStepSize = maxDistance / minMaxMarches.y;

            float stepSize = minStepSize;
            /*if (floor(maxDistance / stepSize) > minMaxMarches.y) {
                stepSize = maxDistance / minMaxMarches.y;
            }
            else if (maxDistance / stepSize < minMaxMarches.x) {
                stepSize = maxDistance / minMaxMarches.x;
            }*/
            float3 startPos = rayOrigin + rayDir * distToContainer;

            float3 coneSamples[] = {
                float3(0.0, 0.1, 0.1),
                float3(0.0, 0.2, 0.2),
                float3(-0.35,0.0, 0.35),
                float3(0.0,-0.5, 0.5),
                float3(0.8,-0.8, 0.8),
                float3(0.0, 0.0, 2.0)
            };
            float coneDepths[] = {
                0.05,
                0.1,
                0.2,
                0.325,
                0.55,
                0.9
            };

            float transmittance = 1;
            float3 lightEnergy = 0;

            float totalDensity = 0;
            while (distTravelled < maxDistance) {
                float3 rayPos = startPos + rayDir * distTravelled;
                float density = max(0, sampleDensity(rayPos));
                if (density > 0) {
                    float lightTransmittance = lightMarch(rayPos, coneDepths);
                    lightEnergy += density * (stepSize + randomOffset) * transmittance * lightTransmittance * phaseVal;
                    transmittance *= exp(-density * (stepSize + randomOffset) * lightAbsorbtionThroughCloud);// *powderEffect(density);

                    if (transmittance < 0.01) {
                        transmittance = 0;
                        break;
                    }
                }
                distTravelled += stepSize + randomOffset;

                float distGradient = distTravelled / maxDistance;
                stepSize = lerp(minStepSize, maxStepSize, pow(clampRemap(distGradient,0.0, 0.7, 0.0, 1.0), 1.0));

                /*if (density < marchDensityThresholds.x) {
                    stepSize = lerp(maxStepSize, minStepSize, saturate(density / max(0.01, marchDensityThresholds.x)));
                }
                else {
                    stepSize = lerp(minStepSize, maxStepSize, (saturate((density - marchDensityThresholds.x) / max(0.01, (marchDensityThresholds.y - marchDensityThresholds.x)))));
                }*/
                //stepSize = lerp(maxStepSize, minStepSize, (saturate((density - marchDensityThresholds.x) / max(0.01, (marchDensityThresholds.y - marchDensityThresholds.x))) - 0.5) * 2);
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
