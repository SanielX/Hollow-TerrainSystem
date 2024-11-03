Shader "Unlit/TerrainPatchesDebugVisuals"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderQueue" = "3500" }

        Pass
        {
            Cull off
            //ZWrite on
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            //#include "Packages/com.hollow.edrp/Shaders/Includes/Basic.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 5.0
            #pragma enable_d3d11_debug_symbols

            #include "../Surface/PhotoTerrainSurface.hlsl"
            #include "../Includes/TerrainInstance.hlsl"
            #include "../Includes/TerrainHeightCommon.hlsl"
            #include "../Editor/PaintingCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                uint lodLevel : VAR_LODLEVEL;
            };

            StructuredBuffer<TerrainPatch> PhotoTerrainLODList;
            StructuredBuffer<uint>         PhotoTerrainVisibleLODList;
            StructuredBuffer<uint>         PhotoTerrainSeamList;
            Texture2D<uint>                LODLevelMap;

            float4x4 unity_MatrixVP;

            v2f vert(appdata v)
            {
                v2f o;
             
                uint nodeIndex = PhotoTerrainVisibleLODList[v.instanceID];
                TerrainPatch patchInfo = PhotoTerrainLODList[nodeIndex];

                PT_Bounds bounds = PT_TerrainPatchBounds(patchInfo);
                float3 size   = (bounds.max - bounds.min);
                float3 center = bounds.min + size *0.5;
                float4x4 objectToWorld = float4x4_TRS(center, float4(0,0,0, 1), size);

                float4 worldPos = mul(objectToWorld, float4(v.vertex.xyz, 1));
                o.vertex = mul(unity_MatrixVP, worldPos);
                o.uv = v.uv;
                o.lodLevel = patchInfo.Lod;

                return o;
            }
            
            float3 hsv(float3 hsv)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
            
                return hsv.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), hsv.y);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = float4(hsv(float3(i.lodLevel / 16.0, 1, 1)), 1);
                return float4(color.rgb, .7);
            }
            ENDHLSL
        }
    }
}
