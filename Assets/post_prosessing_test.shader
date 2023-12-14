Shader "Hidden/post_prosessing_test"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize("Blur Size", Range(0.0, 0.1)) = 0.00
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            uniform float _BlurSize;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 sum = fixed4(0.0, 0.0, 0.0, 0.0);
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y - 4.0 * _BlurSize)) * 0.05;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y - 3.0 * _BlurSize)) * 0.09;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y - 2.0 * _BlurSize)) * 0.12;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y - _BlurSize)) * 0.15;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y)) * 0.16;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y + _BlurSize)) * 0.15;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y + 2.0 * _BlurSize)) * 0.12;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y + 3.0 * _BlurSize)) * 0.09;
				sum += tex2D(_MainTex, half2(i.uv.x, i.uv.y + 4.0 * _BlurSize)) * 0.05;

                return sum;


                fixed4 col = tex2D(_MainTex, i.uv);
                // just invert the colors
                //col.rgb = 1 - col.rgb;
                return col;
            }
            ENDCG
        }
    }
}
