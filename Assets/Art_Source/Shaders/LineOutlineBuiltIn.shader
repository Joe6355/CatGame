Shader "Custom/LineRendererOutlineBuiltIn"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)

        _OutlineSide ("Side Outline Size", Range(0,0.5)) = 0.1
        _OutlineEnd  ("End Outline Size",  Range(0,0.5)) = 0.1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _MainColor;
            fixed4 _OutlineColor;
            float _OutlineSide;
            float _OutlineEnd;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float sideDist = abs(i.uv.y - 0.5);
                float sideOutline = step(0.5 - _OutlineSide, sideDist);

                float endDist = min(i.uv.x, 1.0 - i.uv.x);
                float endOutline = step(endDist, _OutlineEnd);

                float outline = max(sideOutline, endOutline);

                return lerp(_MainColor, _OutlineColor, outline);
            }
            ENDCG
        }
    }
}
