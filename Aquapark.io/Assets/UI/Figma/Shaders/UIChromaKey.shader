// UI shader that makes one colour of a picture transparent (green-screen keying), for the VIP offer's showcase video.
// Used on a RawImage. _KeyColor is the screen colour, _Threshold how close a pixel must be to vanish, _Smoothness the
// soft edge, _Spill how much of the key colour is pulled out of the edges.
Shader "UI/Aqua Chroma Key"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyColor ("Key Colour", Color) = (0,1,0,1)
        _Threshold ("Threshold", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0.001, 0.5)) = 0.08
        _Spill ("Spill Removal", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _KeyColor;
            float _Threshold, _Smoothness, _Spill;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Compare colours by their chroma (YCbCr without brightness), so shadows on the screen key out too.
            float2 Chroma(float3 c)
            {
                return float2(-0.169 * c.r - 0.331 * c.g + 0.5 * c.b, 0.5 * c.r - 0.419 * c.g - 0.081 * c.b);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float d = distance(Chroma(c.rgb), Chroma(_KeyColor.rgb));
                float a = smoothstep(_Threshold * 0.5, _Threshold * 0.5 + _Smoothness, d);
                // Pull the key colour out of the soft edge (green fringe).
                float keyGray = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lerp(c.rgb, keyGray.xxx, _Spill), 1 - a);
                c.a *= a;
                return c * i.color;
            }
            ENDCG
        }
    }
}
