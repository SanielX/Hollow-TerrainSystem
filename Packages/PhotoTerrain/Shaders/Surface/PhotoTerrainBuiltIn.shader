Shader "PhotoTerrain/PTerrain Surface (Built-In)"
{
    Properties
    {
        _BlendOffset("Tri Blend Offset", float) = 0
        _BlendPower ("Tri Blend Power", float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            Tags { "LightMode" = "Deferred" }
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols
            #pragma multi_compile _ PT_AVT_ENABLED
            #pragma multi_compile _ UNITY_HDR_ON
            #pragma multi_compile _ PT_WORLD_DEBUG_MASK
            #pragma multi_compile _ PT_OMPV_RED_MASK PT_OMPV_BACKGROUND_ONLY

            #pragma multi_compile_instancing
            #pragma target 5.0
            #include "UnityStandardUtils.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            #include "PhotoTerrainVertex.hlsl"
            #include "PhotoTerrainFragment.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            float _BlendOffset;
            float _BlendPower;

            UnityLight DummyLight()
            {
                UnityLight light;
                UNITY_INITIALIZE_OUTPUT(UnityLight, light);
        
                light.dir = float3(0,1,0);
                light.color = 0;
        
                return light;
            }

            #define DEFERRED_PASS
            #define UNITY_ENABLE_REFLECTION_BUFFERS 1
            UnityIndirect CreateIndirectLight (VertexOutput i, float3 normal, float3 viewDir, float smoothness, float occlusion) {
                UnityIndirect indirectLight;
                UNITY_INITIALIZE_OUTPUT(UnityIndirect, indirectLight);
        
                #if defined(VERTEXLIGHT_ON)
                    indirectLight.diffuse = i.vertLightColor;
                #endif
                
                #if defined(PASS_FORWARD_BASE) || defined(DEFERRED_PASS)
        
                    #if defined(LIGHTMAP_ON)
                        indirectLight.diffuse = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.lightMapUv));
        
                        #if defined(DIRLIGHTMAP_COMBINED)
        
                            float4 lightmapDirection = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, i.lightMapUv);
                            indirectLight.diffuse = DecodeDirectionalLightmap(indirectLight.diffuse, lightmapDirection, i.wNormal);
        
                        #endif
        
                        // TODO: Speculars from lightmap 
                    #else 
                        indirectLight.diffuse += max(0,ShadeSH9(float4(normal, 1)));  // Adding environmental lighting
                    #endif
        
                    #if defined(DEFERRED_PASS) && UNITY_ENABLE_REFLECTION_BUFFERS
                        indirectLight.specular = 0;     // Deferred gets reflection from it's own pass, so don't bother sampling cubes
                    #else
                        float3 reflectDir = reflect(-viewDir, normal);
        
                        Unity_GlossyEnvironmentData envData;
                        UNITY_INITIALIZE_OUTPUT(Unity_GlossyEnvironmentData, envData);
                        envData.roughness = 1 - smoothness;
                        envData.reflUVW = BoxProjection(reflectDir, i.worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
                        float3 spec0 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, envData);
                        #if UNITY_SPECCUBE_BLENDING
                            envData.reflUVW = BoxProjection(reflectDir, i.wPos, unity_SpecCube1_ProbePosition, unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax);
                            float3 spec1 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1, unity_SpecCube0), unity_SpecCube1_HDR, envData);
                            
                            indirectLight.specular = lerp(spec1, spec0, unity_SpecCube0_BoxMin.w);
                        #else 
                            indirectLight.specular = spec0;
                        #endif
                    #endif
        
                    #if MASK    // Including occlusion
                        indirectLight.diffuse *= occlusion;
                        indirectLight.specular *= occlusion;
                    #endif
                #endif
        
                return indirectLight;
            }
            
            [earlydepthstencil]
            void frag (VertexOutput i,
                out float4 gBuffer0 : SV_TARGET0,  // Color & occlusion
                out float4 gBuffer1 : SV_TARGET1,  // Specular & smoothness
                out float4 gBuffer2 : SV_TARGET2,  // Normals, alpha channel unused but has only 4 values (0, 0.33, 0.66, 1)
                out float4 gBuffer3 : SV_TARGET3)
            {
                gBuffer1 = 0;
                
                TerrainSurfaceInputs ti;
                ti.clipPos        = i.clipPos;
                ti.uv             = i.localUV_Height.xy;
                ti.vertexHeight   = i.localUV_Height.z;
                ti.worldPos       = float4(i.worldPos, 1);
                ti.triBlendOffset = _BlendOffset;
                ti.triBlendPower  = _BlendPower;

                TerrainSurface surf = SampleTerrainSurface(ti);

                //surf.albedo = 0;
                //surf.albedo.rg = debug_lastFetch.virtualUV;
//
                //surf.heightNormal = float3(0,1,0);
                

                float3 specularTint;
                float oneMinusReflectivity;
                surf.albedo.rgb = DiffuseAndSpecularFromMetallic(surf.albedo, surf.metallic, /*out*/ specularTint, /*out*/ oneMinusReflectivity);
                surf.albedo *= oneMinusReflectivity;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                UnityLight light = DummyLight();
                UnityIndirect indirectLight = CreateIndirectLight(i, surf.normal, viewDir, surf.smoothness, 1);
                
                float3 pbr = BRDF3_Unity_PBS(surf.albedo.rgb, specularTint, oneMinusReflectivity, surf.smoothness, surf.normal, viewDir, light, indirectLight);
                //surf.normal = float3(0,1,0);
                //surf.albedo = h.rrr;
                
                gBuffer0 = float4(surf.albedo, 1);
              //  gBuffer1 = float4(specularTint, surf.smoothness);
                gBuffer2 = float4(surf.normal * 0.5 + 0.5, 1);
                gBuffer3.rgb = pbr;
                
                #ifndef UNITY_HDR_ON
                    gBuffer3.rgb = exp2(-gBuffer3.rgb);
                #endif
               // 
               //  gBuffer0 = 0;
                // gBuffer0 = h * 1000;
                //gBuffer3.rg = ti.uv; 
            }
            ENDHLSL
        }

        UsePass "Hidden/PhotoTerrain/Base/VBUFFERPASS"
        UsePass "Hidden/PhotoTerrain/Base/SCENEPICKING"
        UsePass "Hidden/PhotoTerrain/Base/SHADOWCASTER"
        UsePass "Hidden/PhotoTerrain/Base/VIRTUALTEXTUREBLIT"
    }
}
