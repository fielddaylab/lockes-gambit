Shader "SpaceFab/Simple Color Dither"
{
    Properties
    {
		[Header(Colors)] [Space]
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dithering)] [Space]
        _DitherScale ("Dither Scale", Range(1, 32)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

		[Header(Blending)] [Space] 
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend Mode", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DestBlend("Destination Blend Mode", Int) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Operation", Int) = 0
		[Toggle(FD_PREMULTIPLY_ALPHA)] _PremultiplyAlpha("Premultiply Alpha", Float) = 1

		[Header(Culling)] [Space]
		[Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        Lighting Off
        ZWrite Off

        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DestBlend]
        BlendOp [_BlendOp]
        ColorMask [_ColorMask]

        Pass
        {
        CGPROGRAM
            #pragma vertex DefaultUIVert
            #pragma fragment DitherFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
			#pragma shader_feature_local_fragment _ FD_PREMULTIPLY_ALPHA
			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP

            #include "Assets/FieldDay/_Assets/Shaders/CGIncludes/UI.cginc"
            #include "Assets/FieldDay/_Assets/Shaders/CGIncludes/Dithering.cginc"

            half _DitherScale;

            fixed4 DitherFrag(Varyings_UI f, float_vpos screenPos: VPOS) : SV_Target
            {
                f.color.a = Quantize8(f.color.a);
                half4 color = f.color;
    
                UIRectClip(f.mask, color);

                color.a = invstep(GetBayerThreshold8(screenPos.xy / _DitherScale), color.a);
    
                UIAlphaClip(color);
                PremultiplyAlpha(color);
                return color;
            }
        ENDCG
        }
    }
}
