Shader "PoseLandmarkSDK/LowLightEnhancementV2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 3)) = 1.2
        _Contrast ("Contrast", Range(0, 2)) = 1.1
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _Gamma ("Gamma", Range(0.1, 3)) = 0.9
        
        // Advanced properties
        _LocalContrast ("Local Contrast Strength", Range(0, 1)) = 0.3
        _NoiseReduction ("Noise Reduction", Range(0, 1)) = 0.2
        _VignetteCorrection ("Vignette Correction", Range(0, 1)) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0 // Requirement for efficient neighour sampling

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)
            float4 _MainTex_ST;
            
            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Gamma;
            float _LocalContrast;
            float _NoiseReduction;
            float _VignetteCorrection;

            // Helper to get luminance
            float GetLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Simple noise reduction (median-like filter)
                // In low light, camera sensors introduce high-frequency noise
                float3 col = tex2D(_MainTex, i.uv).rgb;
                
                if (_NoiseReduction > 0.05)
                {
                    // Sample 4 neighbors
                    float2 uv_u = i.uv + float2(0, _MainTex_TexelSize.y);
                    float2 uv_d = i.uv - float2(0, _MainTex_TexelSize.y);
                    float2 uv_l = i.uv - float2(_MainTex_TexelSize.x, 0);
                    float2 uv_r = i.uv + float2(_MainTex_TexelSize.x, 0);
                    
                    float3 col_u = tex2D(_MainTex, uv_u).rgb;
                    float3 col_d = tex2D(_MainTex, uv_d).rgb;
                    float3 col_l = tex2D(_MainTex, uv_l).rgb;
                    float3 col_r = tex2D(_MainTex, uv_r).rgb;
                    
                    // Simple average for noise reduction
                    float3 avg = (col + col_u + col_d + col_l + col_r) / 5.0;
                    col = lerp(col, avg, _NoiseReduction);
                }
                
                // --- Local Contrast Enhancement (CLAHE-inspired) ---
                // Enhances details in dark areas without over-exposing bright ones
                if (_LocalContrast > 0.05)
                {
                    // Blur radius for unsharp mask
                    float2 blurOffset = _MainTex_TexelSize.xy * 2.0;
                    
                    // Sample neighborhood to estimate local brightness
                    float3 localMean = col;
                    localMean += tex2D(_MainTex, i.uv + blurOffset).rgb;
                    localMean += tex2D(_MainTex, i.uv - blurOffset).rgb;
                    localMean += tex2D(_MainTex, i.uv + float2(blurOffset.x, -blurOffset.y)).rgb;
                    localMean += tex2D(_MainTex, i.uv + float2(-blurOffset.x, blurOffset.y)).rgb;
                    localMean *= 0.2; // average of 5 samples
                    
                    float localLum = GetLuminance(localMean);
                    
                    // Boost contrast based on local luminance
                    // Dark areas get more boost, bright areas get less
                    float boostFactor = 1.0 + (_LocalContrast * 2.0 * (1.0 - smoothstep(0.5, 1.0, localLum)));
                    col = pow(col, 1.0 / boostFactor);
                }

                // --- Global Adjustments ---
                
                // Gamma correction (lower gamma = brighter darks)
                col = pow(col, _Gamma);
                
                // Brightness
                col *= _Brightness;
                
                // Contrast (around midpoint 0.5)
                col = (col - 0.5) * _Contrast + 0.5;
                
                // Saturation
                float lum = GetLuminance(col);
                col = lerp(float3(lum, lum, lum), col, _Saturation);
                
                // --- Vignette Correction ---
                // Cameras often darken at the corners
                if (_VignetteCorrection > 0.05)
                {
                    float2 dist = i.uv - 0.5;
                    float vignette = 1.0 - dot(dist, dist);
                    col /= lerp(1.0, vignette, _VignetteCorrection);
                }
                
                // Clamp to valid range
                col = saturate(col);
                
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
