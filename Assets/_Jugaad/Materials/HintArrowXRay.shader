Shader "Jugaad/HintArrowXRay"
{
    // Hint arrow: solid where it is in view, see-through where something is in front of it
    // (so an arrow over a wheel inside the arch or a jack under the car is still seen).
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.62, 0.05, 1)
        _HiddenAlpha ("Alpha when hidden", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _HiddenAlpha;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
        Varyings vert (Attributes i)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(i.normalOS);
            return o;
        }
        half Shade (float3 n) { return 0.62h + 0.38h * saturate(dot(normalize(n), normalize(float3(0.3, 0.8, 0.5)))); }
        ENDHLSL

        Pass
        {
            Name "Hidden"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Greater
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target { return half4(_BaseColor.rgb * Shade(i.normalWS), _BaseColor.a * _HiddenAlpha); }
            ENDHLSL
        }

        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target { return half4(_BaseColor.rgb * Shade(i.normalWS), 1); }
            ENDHLSL
        }
    }
}
