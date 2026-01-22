Shader "PoseLandmarkSDK/LowLightEnhancement"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 2)) = 1.2
        _Contrast ("Contrast", Range(0, 2)) = 1.1
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _Gamma ("Gamma", Range(0.1, 3)) = 0.9
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
            float4 _MainTex_ST;
            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Gamma;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Apply gamma correction (lower gamma = brighter darks)
                col.rgb = pow(col.rgb, _Gamma);
                
                // Apply brightness
                col.rgb *= _Brightness;
                
                // Apply contrast (around midpoint 0.5)
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                
                // Apply saturation
                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(float3(luminance, luminance, luminance), col.rgb, _Saturation);
                
                // Clamp to valid range
                col.rgb = saturate(col.rgb);
                
                return col;
            }
            ENDCG
        }
    }
}
