// URP port of the ML-Agents example grid shader. Same shader name, same property names,
// so the existing example materials keep working. The original was a built-in surface shader
// (Standard lighting, metallic 0, smoothness 0) that draws grid lines over a cell color and
// discards pixels whose color alpha is exactly 0.
Shader "ML-Agents/GridPattern"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _CellColor ("Cell Color", Color) = (0,0,0,0)
        [PerRendererData] _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [IntRange] _GridSize ("Grid Size", Range(1,100)) = 10
        _LineSize ("Line Size", Range(0,1)) = 0.15
        [IntRange] _DrawU ("Draw U Toggle ( 0 = False , 1 = True )", Range(0,1)) = 1
        [IntRange] _DrawV ("Draw V Toggle ( 0 = False , 1 = True )", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _LineColor;
                float4 _CellColor;
                float _GridSize;
                float _LineSize;
                float _DrawU;
                float _DrawV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Grid pattern, identical to the original surface shader.
                float2 uv = IN.uv;
                float gsize = floor(_GridSize) + _LineSize;
                float4 color = _CellColor;
                float brightness = _CellColor.a;

                if (round(_DrawU) == 1.0 && frac(uv.x * gsize) <= _LineSize)
                {
                    color = _LineColor;
                    brightness = _LineColor.a;
                }
                if (round(_DrawV) == 1.0 && frac(uv.y * gsize) <= _LineSize)
                {
                    color = _LineColor;
                    brightness = _LineColor.a;
                }
                if (brightness == 0.0)
                {
                    clip(-1.0);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = color.rgb * brightness;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = 0.0;
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);

                half4 result = UniversalFragmentPBR(inputData, surfaceData);
                result.rgb = MixFog(result.rgb, IN.fogFactor);
                return result;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
