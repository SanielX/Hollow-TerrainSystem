Shader "Hidden/PhotoTerrainEditor/SmoothHeight"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off
        ZWrite Off
        ZTest Always
        ZClip off
        Blend off

        HLSLINCLUDE
        #pragma enable_d3d11_debug_symbols
        #include "../Painting/Brush.hlsl"
        
        Texture2D    _HeightTex;
        float4       _HeightTex_TexelSize;
        SamplerState sampler_HeightTex;

        float2 _BlurDirection;
        int    _KernelSize;

        float _SmoothDirection;

        float frag(BrushPixelInput i) : SV_Target
        {
            uint hw,hh;
            _HeightTex.GetDimensions(hw, hh);

            float2 screenUV = i.vertex.xy / float2(hw,hh);
            float color = _HeightTex.Sample(sampler_HeightTex, screenUV).r;
            
            float  brushStrength = SampleBrush(i.worldPosition, i.uv0);

            float d = 1;
            float h = color;
			for(int x = 0; x < _KernelSize; ++x)
			{
                float2 offset = _HeightTex_TexelSize.xy * _BlurDirection * (x + 1);
			    float weight = (float)(_KernelSize - x) / (float)(_KernelSize + 1);

				h += _HeightTex.Sample(sampler_HeightTex, screenUV + offset) * weight;
				h += _HeightTex.Sample(sampler_HeightTex, screenUV - offset) * weight;
			    
			    d += weight * 2;
			}

            h /= d;
            float direction = _SmoothDirection;
            float3 smoothheights = float3(saturate(1-abs(direction)), saturate(-direction), saturate(direction));
			h = dot(float3(h, min(h, color), max(h, color)), smoothheights);
            
            return lerp(color, h, brushStrength);
        }
        ENDHLSL

        Pass
        {   
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {   
            HLSLPROGRAM
            #pragma vertex BrushVert
            #pragma fragment frag_gpudriven
			
            StructuredBuffer<TerrainTextureRegion> _TerrainTextureRegions;
            Texture2D _HeightmapHistory;
            SamplerState sampler_HeightmapHistory;
            float4 _HeightmapHistory_TexelSize;
            
            SamplerState point_clamp_sampler;
            float4 _ScreenSize;

            float2 unlerp(float2 a, float2 b, float2 x)
            {
                return (x - a) / (b - a);
            }
            
        	float frag_gpudriven(BrushPixelInput i) : SV_Target
        	{
        		float4 regionBounds = BrushStateBuffer[0].brushBounds;
        	    float2 screenUV = i.vertex.xy * _HeightmapHistory_TexelSize.xy;
        	    float color = _HeightmapHistory.SampleLevel(sampler_HeightmapHistory, screenUV, 0).r;
        		// return color;
        	    
        	    float  brushStrength = SampleBrush(i.worldPosition, i.uv0);
	
        	    float d = 1;
        	    float h = color;
        		const uint _KernelSize = 8;
				for(int x = 0; x < _KernelSize; ++x)
				{
        	        float2 offset = _HeightmapHistory_TexelSize.xy * _BlurDirection * (x + 1);
				    float weight = (float)(_KernelSize - x) / (float)(_KernelSize + 1);
	
					h += _HeightmapHistory.SampleLevel(sampler_HeightmapHistory, screenUV + offset, 0) * weight;
					h += _HeightmapHistory.SampleLevel(sampler_HeightmapHistory, screenUV - offset, 0) * weight;
				    
				    d += weight * 2;
				}
	
        	    h /= d;
        	    float direction = _SmoothDirection;
        	    float3 smoothheights = float3(saturate(1-abs(direction)), saturate(-direction), saturate(direction));
				h = dot(float3(h, min(h, color), max(h, color)), smoothheights);
        	    
        	    return lerp(color, h, brushStrength);
        	}
            ENDHLSL
        }
    }
}