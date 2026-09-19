Shader "ArknightsACT/ImportedClientFX"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Amount ("Dissolve Amount", Range(0,1)) = 0
        _BorderWidth ("Dissolve Border", Range(0,1)) = 0.1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Toggle] _ZWrite ("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            Name "ImportedClientFX"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Keep this shader usable in both the project's current built-in
            // renderer and a future URP asset.  A RenderPipeline tag would make
            // the whole subshader invisible while GraphicsSettings has no
            // active URP asset.
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _DissolveTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DissolveTex_ST;
                half4 _TintColor;
                half4 _Color;
                float _Amount;
                float _BorderWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = tex2D(_MainTex, input.uv);
                baseColor *= _TintColor * _Color * input.color;

                // Several client materials intentionally use One/Zero blend
                // (opaque/additive-looking) while their source textures still
                // carry meaningful alpha.  Without an alpha test those
                // transparent texels become a solid quad in the fallback
                // shader, hiding the actual blade/glow silhouette.
                clip(baseColor.a - 0.001);

                if (_Amount > 0.0001)
                {
                    float2 dissolveUV = input.uv * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                    half dissolve = tex2D(_DissolveTex, dissolveUV).r;
                    clip(dissolve - _Amount);
                    half edge = saturate((_Amount + max(_BorderWidth, 0.0001) - dissolve) / max(_BorderWidth, 0.0001));
                    baseColor.rgb += edge * baseColor.rgb;
                }

                return baseColor;
            }
            ENDHLSL
        }
    }
}
