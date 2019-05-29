Shader "Hidden/VectorShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Pass
		{
			CGPROGRAM
			#pragma target 5.0
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
				float4 screenPos: TEXCOORD1;
				float4 cameraRay : TEXCOORD2;

			};

			struct Node {
				float3 color;
				float3 normal;
				float depth;
				uint next;
				float3 v0;
				float3 v1;
				float3 v2;
				float3 mat;
				float var;
				bool visited;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			sampler2D _CameraGBufferTexture0;	// Diffuse color (RGB), unused (A)
			sampler2D _CameraGBufferTexture1;	// Specular color (RGB), roughness (A)
			sampler2D _CameraGBufferTexture2;	// World space normal (RGB), unused (A)
			sampler2D _CameraGBufferTexture3;	// ARGBHalf (HDR) format: Emission + lighting + lightmaps + reflection probes buffer

			sampler2D _PrevFrame;

			float4 _Color;

			RWStructuredBuffer<Node> list : register(u1);
			//RWByteAddressBuffer head : register(u2);
			Buffer<uint> head : register(t2);

			float _nearClip;

			int width;
			float4 resolution;

			sampler2D _CameraDepthTexture;

			float4x4 _CameraProjectionMatrix;			// projection matrix that maps to screen pixels (not NDC)
			float4x4 _CameraInverseProjectionMatrix;	// inverse projection matrix (NDC to camera space)
			float _BinarySearchIterations;				// maximum binary search refinement iterations
			float _PixelZSize;							// Z size in camera space of a pixel in the depth buffer

			float4x4 _NormalMatrix;
			float2 _RenderBufferSize;
			float2 _OneDividedByRenderBufferSize;		// Optimization: removes 2 divisions every itteration

			uint _loopMax;
			float _sortLayers;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.screenPos = ComputeScreenPos(o.vertex);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				// uv kordinater
				float2 uv = i.screenPos.xy / i.screenPos.w;
				uint2 screenpos = (0.5 * (uv + 1.0)) * _ScreenParams.xy;
				
				// Siden head pointer er 1d
				uint bufferPos = width * screenpos.y + screenpos.x;
				//uint bufferPos = 4*((width * screenpos.y) + screenpos.x);

				// index verdien lagret i head pointer bufferet
				uint index = head.Load(bufferPos);

				uint startIndex = head.Load(bufferPos);

				Node n = list[index];

				if (_sortLayers == 1) {

					Node startNode = list[startIndex];
					while (startNode.visited && startIndex != 0xffffffff)
					{
						for (int i = 0; i < 4; i++)
						{
							if (startIndex != 0xffffffff)
							{
								startIndex = startNode.next;
								startNode = list[startIndex];
							}
						}
					}

					uint lowestIndex = startIndex;

					while (index != 0xffffffff)
					{
						n = list[index];

						if (!n.visited && n.depth < list[lowestIndex].depth)
						{
							lowestIndex = index;
						}

						for (int i = 0; i < 4; i++)
						{
							index = n.next;
							if (index != 0xffffffff)
							{
								n = list[index];
							}
							else {
								break;
							}
						}
					}

					list[lowestIndex].visited = true;

					Node lowest = list[lowestIndex];

					return float4(lowest.color, 1);
				}
				else {

					// For å bla igjennom listen. Hopp med 4, altså i < 0/4/8/12
					Node n = list[index];
					for (int i = 0; i < _loopMax; i++) {

						//Få indexen til neste elementet i den kjedete listen
						index = n.next;

						// neste noden i listen
						n = list[index];

						if (index == -1)
						{
							break;
						}
					}

					return float4(n.color, 1);
				}
			}
				ENDCG
		}
	}
}