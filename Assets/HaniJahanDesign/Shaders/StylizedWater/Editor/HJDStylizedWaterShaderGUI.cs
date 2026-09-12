using UnityEditor;
using UnityEngine;

namespace HaniJahanDesign.StylizedWaterShaderPack
{
    public sealed class HJDStylizedWaterShaderGUI : ShaderGUI
    {
        private bool showAdvancedRipples;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawSurface(materialEditor, properties);
            DrawRipples(materialEditor, properties);
            DrawFoam(materialEditor, properties);
            DrawWaves(materialEditor, properties);
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
        }

        private static void DrawSurface(MaterialEditor editor, MaterialProperty[] properties)
        {
            BeginSection("Water Surface", "Set the main colors and overall visibility.");
            editor.ShaderProperty(FindProperty("_BaseColor", properties), new GUIContent("Base Color"));
            MaterialProperty secondColor = FindProperty("_EnableSecondColor", properties);
            editor.ShaderProperty(secondColor, new GUIContent("Use Second Color", "Adds a shallow-water color using world-space height."));
            if (IsEnabled(secondColor))
            {
                editor.ShaderProperty(FindProperty("_ShallowColor", properties), new GUIContent("Shallow Color"));
                editor.ShaderProperty(FindProperty("_SecondColorHeight", properties), new GUIContent("Height", "Moves the center of the color transition along world Y."));
                editor.ShaderProperty(FindProperty("_SecondColorSpread", properties), new GUIContent("Spread", "Controls how gradually the two colors blend."));
            }
            editor.ShaderProperty(FindProperty("_TransparencyMultiplier", properties), new GUIContent("Opacity", "Controls the overall water opacity."));

            MaterialProperty fresnel = FindProperty("_EnableFresnel", properties);
            editor.ShaderProperty(fresnel, new GUIContent("Edge Highlight", "Makes water more visible at glancing angles."));
            if (IsEnabled(fresnel))
            {
                editor.ShaderProperty(FindProperty("_FresnelPower", properties), new GUIContent("Edge Size"));
            }
            EndSection();
        }

        private void DrawRipples(MaterialEditor editor, MaterialProperty[] properties)
        {
            BeginSection("Surface Ripples", "Add moving detail across the water surface.");
            MaterialProperty toggle = FindProperty("_EnableRipples", properties);
            editor.ShaderProperty(toggle, new GUIContent("Enabled"));
            if (IsEnabled(toggle))
            {
                MaterialProperty source = FindProperty("_RippleSource", properties);
                editor.ShaderProperty(source, new GUIContent("Style", "Procedural cells or a custom texture."));
                int sourceValue = source.hasMixedValue ? -1 : Mathf.RoundToInt(source.floatValue);

                if (sourceValue == 1 || sourceValue == -1)
                {
                    editor.TexturePropertySingleLine(new GUIContent("Texture", "Uses the red channel."), FindProperty("_RippleTex", properties));
                }
                editor.ShaderProperty(FindProperty("_RippleColor", properties), new GUIContent("Color"));
                editor.ShaderProperty(FindProperty("_RippleScale", properties), new GUIContent("Scale"));
                editor.ShaderProperty(FindProperty("_RippleSpeed", properties), new GUIContent("Speed"));
                editor.ShaderProperty(FindProperty("_RippleStrength", properties), new GUIContent("Strength"));
                showAdvancedRipples = EditorGUILayout.Foldout(showAdvancedRipples, "Advanced", true);
                if (showAdvancedRipples)
                {
                    editor.ShaderProperty(FindProperty("_RippleSharpness", properties), new GUIContent("Sharpness"));
                }
            }
            EndSection();
        }

        private static void DrawFoam(MaterialEditor editor, MaterialProperty[] properties)
        {
            BeginSection("Intersection Foam", "Draw foam where water meets shorelines and objects.");
            MaterialProperty toggle = FindProperty("_EnableFoam", properties);
            editor.ShaderProperty(toggle, new GUIContent("Enabled"));
            if (IsEnabled(toggle))
            {
                EditorGUILayout.LabelField("Primary Foam (Gradient)", EditorStyles.boldLabel);
                editor.ShaderProperty(FindProperty("_DepthThreshold", properties), new GUIContent("Reach", "How far foam extends from intersections."));
                editor.ShaderProperty(FindProperty("_FoamIntensity", properties), new GUIContent("Strength"));

                MaterialProperty secondFoam = FindProperty("_EnableSecondFoam", properties);
                editor.ShaderProperty(secondFoam, new GUIContent("Add Second Foam", "Layers independently controlled banded or textured foam over the primary gradient."));
                if (IsEnabled(secondFoam))
                {
                    EditorGUILayout.LabelField("Second Foam", EditorStyles.boldLabel);
                    MaterialProperty source = FindProperty("_SecondFoamSource", properties);
                    editor.ShaderProperty(source, new GUIContent("Style", "Use procedural layered bands or a scrolling texture."));
                    int sourceValue = source.hasMixedValue ? -1 : Mathf.RoundToInt(source.floatValue);

                    editor.ShaderProperty(FindProperty("_SecondFoamColor", properties), new GUIContent("Color"));
                    editor.ShaderProperty(FindProperty("_SecondFoamDepthThreshold", properties), new GUIContent("Reach", "How far the second foam extends from intersections."));
                    editor.ShaderProperty(FindProperty("_SecondFoamIntensity", properties), new GUIContent("Strength"));

                    if (sourceValue == 0 || sourceValue == -1)
                    {
                        editor.ShaderProperty(FindProperty("_SecondFoamLineCount", properties), new GUIContent("Line Count"));
                        editor.ShaderProperty(FindProperty("_SecondFoamLineThickness", properties), new GUIContent("Line Thickness"));
                        editor.ShaderProperty(FindProperty("_SecondFoamEdgeSoftness", properties), new GUIContent("Edge Softness"));
                    }

                    if (sourceValue == 1 || sourceValue == -1)
                    {
                        editor.TexturePropertySingleLine(new GUIContent("Texture", "Uses the red channel."), FindProperty("_SecondFoamTex", properties));
                        editor.ShaderProperty(FindProperty("_SecondFoamScale", properties), new GUIContent("Scale"));
                        editor.ShaderProperty(FindProperty("_SecondFoamSpeed", properties), new GUIContent("Speed"));
                    }
                }
            }
            EndSection();
        }

        private static void DrawWaves(MaterialEditor editor, MaterialProperty[] properties)
        {
            BeginSection("Vertex Waves", "Move the mesh vertices to create broad surface waves.");
            MaterialProperty toggle = FindProperty("_EnableVertexWaves", properties);
            editor.ShaderProperty(toggle, new GUIContent("Enabled"));
            if (IsEnabled(toggle))
            {
                editor.ShaderProperty(FindProperty("_WaveHeight", properties), new GUIContent("Height"));
                editor.ShaderProperty(FindProperty("_WaveScale", properties), new GUIContent("Scale"));
                editor.ShaderProperty(FindProperty("_WaveSpeed", properties), new GUIContent("Speed"));
                EditorGUILayout.HelpBox("Use a subdivided mesh so vertex waves have enough geometry to deform.", MessageType.Info);
            }
            EndSection();
        }

        private static bool IsEnabled(MaterialProperty toggle)
        {
            return toggle.hasMixedValue || toggle.floatValue >= 0.5f;
        }

        private static void BeginSection(string title, string description)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2f);
        }

        private static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }
    }
}
