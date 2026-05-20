Shader "Custom/URP_VertexSplatTerrain"
{
    Properties
    {
        _MainTex ("Base Texture (Black - Grass)", 2D) = "white" {}
        _RedTex ("Red Channel (Dirt)", 2D) = "white" {}
        _GreenTex("Green Channel (Rock)", 2D) = "white" {}
        _BlueTex("Blue Channel (Sand)", 2D) = "white" {}
        _Tiling ("Tiling", Float) = 20.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORDA; // custom
            };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_RedTex);    SAMPLER(sampler_RedTex);
            TEXTURE2D(_GreenTex);  SAMPLER(sampler_GreenTex);
            TEXTURE2D(_BlueTex);   SAMPLER(sampler_BlueTex);

            CBUFFER_START(UnityPerMaterial)
                float _Tiling;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                // World space tiling is better for chunks
                output.uv = output.positionWS.xz * (1.0 / _Tiling);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 colBase  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 colRed   = SAMPLE_TEXTURE2D(_RedTex,  sampler_RedTex,  input.uv);
                half4 colGreen = SAMPLE_TEXTURE2D(_GreenTex,sampler_GreenTex,input.uv);
                half4 colBlue  = SAMPLE_TEXTURE2D(_BlueTex, sampler_BlueTex, input.uv);

                half r = input.color.r;
                half g = input.color.g;
                half b = input.color.b;
                half baseWeight = saturate(1.0 - (r + g + b));

                half3 finalColor = colBase.rgb * baseWeight + 
                                   colRed.rgb * r + 
                                   colGreen.rgb * g + 
                                   colBlue.rgb * b;

                // Simple main lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(input.normalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL + half3(0.3, 0.3, 0.3);

                return half4(finalColor * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
