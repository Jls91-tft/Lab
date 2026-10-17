// Multiplies what is already on screen by a light map (D-024): where the map is white nothing changes, where it is dark the
// world goes dark. The map is a small texture with one texel per tile (PrototypeLighting), stretched bilinearly over the view.
// One pass without a LightMode tag, so the 2D renderer draws it once (SRPDefaultUnlit); a second pass would square the darkness.
Shader "CepaCero/LightMultiply"
{
    Properties
    {
        [MainTexture] _MainTex ("Light map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend DstColor Zero
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
