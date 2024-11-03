Shader "PhotoTerrain/Terrain Decal"
{
	Properties
	{
		_DecalMask ("Decal Mask", 2D) = "white" {}
		_DecalMaskMult("Decal Mask Multiplier", float) = 1
		
		_MainTex ("Texture", 2D) = "white" {}
		_NormalTex ("Normal Map", 2D) = "bump" {}
		_MaskTex ("Mask Map", 2D) = "bump" {}
		
		_NormalStrength("Normal Map Strength", float) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		ZWrite off
		ZTest always
		ZClip off
		Cull off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM
			#pragma target 2.0

			#pragma enable_d3d11_debug_symbols
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "UnityStandardUtils.cginc"
			#include "Includes/TerrainInstance.hlsl"
			#include "Includes/TerrainHeightCommon.hlsl"
			#include "Includes/VirtualTexture.hlsl"

			struct VertexOutput
			{
				float4 clipPos : SV_Position;
				float3 worldPos : VAR_WORLDPOS;
				float3 worldNormal : VAR_WORLDNORMAL;
				float4 worldTangent : VAR_WORLDTANGENT;
				float2 uv : TEXCOORD0;
				float2 terrain_uv : TEXCOORD1;
			};

			float _NormalStrength;
			Texture2D _NormalTex;
			Texture2D _MaskTex;
			Texture2D _DecalMask;

			Texture2D _MainTex;
			SamplerState sampler_MainTex;
			SamplerState sampler_DecalMask;

			float _DecalMaskMult;

			SamplerState sampler_linear_clamp;
			VertexOutput vert(float3 position : POSITION, float2 uv : TEXCOORD0, float4 tangent : TANGENT, float3 normal : NORMAL)
			{
				VertexOutput o;
				o.uv = uv;

				float3 worldPosition = mul(unity_ObjectToWorld, float4(position, 1));
				o.clipPos    = mul(unity_MatrixVP, float4(worldPosition, 1));
				o.worldPos   = worldPosition;
				o.terrain_uv = (worldPosition.xz - PT_TerrainInstancePosition.xz) * PT_TerrainInstanceSizeRcp.xz;

				o.worldNormal  = mul(unity_ObjectToWorld, float4(normal.xyz, 0)).xyz;
				o.worldTangent = float4(mul(unity_ObjectToWorld, float4(tangent.xyz, 0)).xyz, tangent.w);

				return o;
			}
			
			float3x3 CreateTangentToWorld(float3 normal, float3 tangent, float flipSign)
			{
			    // For odd-negative scale transforms we need to flip the sign
			    float sgn = flipSign;
			    float3 bitangent = cross(normal, tangent) * sgn;
			
			    return float3x3(tangent, bitangent, normal);
			}
						
			
			float3 TransformTangentToWorld(float3 dirTS, float3x3 tangentToWorld)
			{
			    // Note matrix is in row major convention with left multiplication as it is build on the fly
			    return mul(dirTS, tangentToWorld);
			}
						
			float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS)
			{
			    float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
			    return normalize(TransformTangentToWorld(normalTS, tangentToWorld));
			}

			SamplerState sampler_NormalTex;
			void frag(VertexOutput i, out float4 albedo : SV_Target0, out float4 vt1 : SV_Target1, out float4 vt2 : SV_Target2)
			{
				i.worldNormal = normalize(i.worldNormal);
				i.worldTangent = normalize(i.worldTangent);
				
				float height = SampleTerrainHeightmap(i.terrain_uv);
				float worldY = height * PT_TerrainInstanceSize.y;

				float diff = max(0, worldY - i.worldPos.y - .2);
				diff = diff * 20.0;

				float4 tangentNormalSample = _NormalTex.Sample(sampler_NormalTex, i.uv);

        		float normalStrength = _NormalStrength;
        		float3 tangentNormal = UnpackNormalWithScale(tangentNormalSample, normalStrength);
        		float3 normal		 = NormalTangentToWorld(tangentNormal, i.worldNormal, i.worldTangent);
				// if(i.worldPos.y < worldY) clip(-1);
				
				albedo = _MainTex.Sample(sampler_MainTex, i.uv);
				//albedo = diff;
			//	albedo.a *= 1 - saturate(diff);
				albedo.a *= saturate(_DecalMask.Sample(sampler_DecalMask, i.uv).r * _DecalMaskMult);

				VTSurface surf;
				surf.albedo     = albedo.rgb;
				surf.alpha      = albedo.a;
				surf.normal     = normal;
				surf.metallic   = 0;
				surf.smoothness = 0;
                PackSurfaceToVT(surf, albedo, vt1, vt2);
			}
			ENDHLSL
		}
	}
}