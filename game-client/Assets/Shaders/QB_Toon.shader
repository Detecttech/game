Shader "QuizBattle/Toon"
{
    // Hand-written URP toon/cel shader: banded diffuse with a cool shadow tint, a
    // lit-side rim light, a hard specular blob, and an inverted-hull outline pass.
    // Driven by flat colors by default so it works with the project's procedurally-
    // generated meshes; _USE_MAINTEX opts individual materials into sampling a base
    // map (multiplied into the flat color) for meshes that do carry UVs.
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Base Map", 2D) = "white" {}
        _ShadowTint("Shadow Tint", Color) = (0.72, 0.74, 0.88, 1)
        _ShadeThreshold("Shade Threshold", Range(0.05, 0.95)) = 0.42
        _ShadeSmoothness("Shade Band Smoothness", Range(0.001, 0.35)) = 0.08

        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.8
        _RimIntensity("Rim Intensity", Range(0, 4)) = 0.75
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.35

        _SpecTint("Specular Color", Color) = (1, 1, 1, 1)
        _Gloss("Glossiness", Range(4, 256)) = 32
        _SpecIntensity("Specular Intensity", Range(0, 2)) = 0.45

        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 0

        _OutlineColor("Outline Color", Color) = (0.10, 0.08, 0.16, 1)
        _OutlineWidth("Outline Width", Range(0, 6)) = 0.9

        [Toggle(_OUTLINE_ON)] _OutlineToggle("Enable Outline", Float) = 1
        [Toggle(_VERTEX_COLORS)] _VertexColorsToggle("Use Vertex Colors", Float) = 0
        [Toggle(_USE_MAINTEX)] _UseMainTexToggle("Use Base Map", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Shared across every pass so the UnityPerMaterial CBUFFER layout is identical
        // everywhere — required for SRP Batcher compatibility.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _MainTex_ST;
            half4 _ShadowTint;
            half _ShadeThreshold;
            half _ShadeSmoothness;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _RimThreshold;
            half4 _SpecTint;
            half _Gloss;
            half _SpecIntensity;
            half4 _EmissionColor;
            half _EmissionIntensity;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_local _ _OUTLINE_ON
            #pragma multi_compile_instancing
            #pragma target 3.0

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

            #if defined(_OUTLINE_ON)
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                // Subtle, clean cartoon contour outline
                float3 normalCS = TransformWorldToHClipDir(normalWS, true);
                positionCS.xy += normalCS.xy * _OutlineWidth * 0.0022 * positionCS.w;
                output.positionCS = positionCS;
            #else
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            #endif
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
            #if defined(_OUTLINE_ON)
                return _OutlineColor;
            #else
                clip(-1);
                return 0;
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile_local _ _USE_MAINTEX
            #pragma multi_compile_local _ _VERTEX_COLORS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = posInputs.positionWS;
                output.positionCS = posInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 baseColor = _BaseColor.rgb;
            #if defined(_VERTEX_COLORS)
                baseColor *= input.color.rgb;
            #endif
            #if defined(_USE_MAINTEX)
                baseColor *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
            #endif

                // Clash Royale vibrant Half-Lambert wrapped diffuse lighting
                half ndl = dot(normalWS, mainLight.direction);
                half wrappedNdl = saturate(ndl * 0.5 + 0.5);
                half shadowFactor = lerp(0.35, 1.0, mainLight.shadowAttenuation);
                half rawLit = wrappedNdl * shadowFactor;

                // Smooth cartoon cel ramp
                half litAmount = smoothstep(_ShadeThreshold - _ShadeSmoothness, _ShadeThreshold + _ShadeSmoothness, rawLit);

                // Vibrant sunlit diffuse + soft ambient shadow (never dirty black)
                half3 shadowTinted = baseColor * _ShadowTint.rgb;
                half3 diffuse = lerp(shadowTinted, baseColor, litAmount) * mainLight.color;

                // Luminous sky & grass bounce ambient
                half3 ambient = SampleSH(normalWS) * baseColor * 0.65;

                // Clash Royale studio rim backlight on character silhouette
                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                rim *= smoothstep(_RimThreshold - 0.15, _RimThreshold + 0.15, rim);
                half3 rimResult = rim * _RimColor.rgb * _RimIntensity * (0.4 + 0.6 * saturate(ndl));

                // Shiny glossy toy specular highlight
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half ndh = saturate(dot(normalWS, halfDir));
                half specRaw = pow(ndh, _Gloss);
                half specBand = smoothstep(0.28, 0.55, specRaw) * step(-0.1, ndl);
                half3 specResult = specBand * _SpecTint.rgb * _SpecIntensity;

                half3 additional = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                int addCount = GetAdditionalLightsCount();
                for (int i = 0; i < addCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addNdl = saturate(dot(normalWS, addLight.direction));
                    additional += addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation * addNdl) * baseColor;
                }
            #endif

                half3 emission = _EmissionColor.rgb * _EmissionIntensity;

                half3 color = diffuse + ambient + rimResult + specResult + additional + emission;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
