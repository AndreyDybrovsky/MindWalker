Shader "Custom/PeepholeVignette"
{
    Properties
    {
        _Radius   ("Radius",   Range(0, 2))     = 0.5
        _Softness ("Softness", Range(0.001, 0.3)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay+1" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f   { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            float _Radius;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv - float2(0.5, 0.5);
                uv.x *= _ScreenParams.x / _ScreenParams.y;
                float dist = length(uv);
                float alpha = smoothstep(_Radius - _Softness, _Radius + _Softness, dist);
                return fixed4(0.0, 0.0, 0.0, alpha);
            }
            ENDCG
        }
    }
}
