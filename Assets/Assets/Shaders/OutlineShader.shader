Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.8, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+1" }

        Pass
        {
            Name "Outline"
            Cull Front          // рисуем только обратные грани — снаружи объекта
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _OutlineColor;
            float  _OutlineWidth;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                // Выдвигаем вершины наружу по нормали
                float3 expandedPos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(expandedPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}