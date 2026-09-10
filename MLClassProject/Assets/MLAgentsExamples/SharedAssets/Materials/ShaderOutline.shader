// URP port of the ML-Agents example outline shader. Same shader name and properties.
// Pass 1 (untagged, so URP runs it as SRPDefaultUnlit): inverted-hull outline.
// Pass 2 (UniversalForward): screen-space scrolling texture.
Shader "Custom/Outline and ScreenSpace texture"
{
    Properties
    {
        [Header(Outline)]
        _OutlineVal ("Outline value", Range(0., 2.)) = 1.
        _OutlineCol ("Outline color", color) = (1., 1., 1., 1.)
        [Header(Texture)]
        _MainTex ("Texture", 2D) = "white" {}
        _Zoom ("Zoom", Range(0.5, 20)) = 1
        _SpeedX ("Speed along X", Range(-1, 1)) = 0
        _SpeedY ("Speed along Y", Range(-1, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float  _OutlineVal;
            half4  _OutlineCol;
            float  _Zoom;
            float  _SpeedX;
            float  _SpeedY;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        ENDHLSL

        Pass
        {
            Name "Outline"
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

                // Normal in view space, then scaled into clip space like the original.
                float3 normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normalOS);
                normal.x *= UNITY_MATRIX_P[0][0];
                normal.y *= UNITY_MATRIX_P[1][1];
                o.positionCS.xy += _OutlineVal * normal.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _OutlineCol;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScreenSpaceTexture"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // SV_POSITION in the fragment stage is the pixel position, same as the old VPOS.
                float2 screenUV = i.positionCS.xy / _ScreenParams.xy;
                float2 uv = (screenUV + float2(_Time.y * _SpeedX, _Time.y * _SpeedY)) / _Zoom;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
