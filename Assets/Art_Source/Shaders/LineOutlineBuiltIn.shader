Shader "Custom/LineRendererOutlineBuiltIn_BentEnd"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)

        _OutlineSide ("Side Outline Size", Range(0,0.5)) = 0.1
        _OutlineEnd  ("End Outline Size",  Range(0,0.5)) = 0.1

        _EndBend ("End Bend (0 straight, 1 semicircle)", Range(0,1)) = 0
        _EndRadius ("End Radius Scale", Range(0.1,2)) = 1
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
            float _EndBend;
            float _EndRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // --- SIDE OUTLINE (unchanged)
                float sideDist = abs(uv.y - 0.5);
                float sideOutline = step(0.5 - _OutlineSide, sideDist);

                // --- END BENDING (core change)

                // remap x from [0..1] into [-1..1]
                float x = (uv.x - 0.5) * 2.0;

                // bend factor: how much we curve into arc
                float bend = saturate(_EndBend);

                // arc shaping (sin-based semicircle deformation)
                // when bend=1 -> full semicircle mapping
                float arcX = sin(x * 1.5707963); // PI/2

                float blendedX = lerp(x, arcX, bend);

                // restore to [0..1]
                float curvedU = blendedX * 0.5 + 0.5;

                // radius compression for tighter arc
                float radius = lerp(1.0, _EndRadius, bend);

                // END DISTANCE now based on curved space
                float endDist = min(curvedU, 1.0 - curvedU * radius);

                float endOutline = step(endDist, _OutlineEnd);

                float outline = max(sideOutline, endOutline);

                return lerp(_MainColor, _OutlineColor, outline);
            }
            ENDCG
        }
    }
}