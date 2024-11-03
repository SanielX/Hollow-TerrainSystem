using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Hollow.Extensions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowEditor.TerrainSystem
{
[CustomEditor(typeof(TerrainTextureSetCollectionImporter))]
public class TerrainTextureSetCollectionImporterEditor : AssetImporterEditor
{
    private static readonly MethodInfo getHelpIconsMethod =
        typeof(EditorGUIUtility).GetMethod("GetHelpIcon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    SerializedProperty
        AlbedoFormat,
        AlbedoResolution,
        NormalFormat,
        NormalResolution,
        MaskFormat,
        MaskResolution,
        TextureSets;

    ReorderableList textureSetsList;
    StringBuilder   report = new();

    public override void OnEnable()
    {
        base.OnEnable();

        AlbedoFormat     = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.AlbedoFormat));
        AlbedoResolution = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.AlbedoResolution));
        NormalFormat     = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.NormalFormat));
        NormalResolution = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.NormalResolution));
        MaskFormat       = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.MaskFormat));
        MaskResolution   = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.MaskResolution));
        TextureSets      = serializedObject.FindProperty(nameof(TerrainTextureSetCollectionImporter.TextureSets));

        textureSetsList                    = new(serializedObject, TextureSets, true, true, true, true);
        textureSetsList.drawHeaderCallback = rect => EditorGUI.PrefixLabel(rect, new("Texture Sets"));

        textureSetsList.onAddCallback = list =>
        {
            list.serializedProperty.arraySize++;
            var element    = textureSetsList.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);

            var nameProp   = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Name));
            var albedoProp = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Albedo));
            var normalProp = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Normal));
            var maskProp   = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Mask));

            nameProp.stringValue = "";
            albedoProp.objectReferenceValue = null;
            normalProp.objectReferenceValue = null;
            maskProp  .objectReferenceValue = null;

            var id = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.GUID));
            id.stringValue = System.Guid.NewGuid().ToString();

            textureSetsList.serializedProperty.serializedObject.ApplyModifiedProperties();
        };

        textureSetsList.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2 + 100f;
        textureSetsList.drawElementCallback = (rect, index, active, focused) =>
        {
            var element    = textureSetsList.serializedProperty.GetArrayElementAtIndex(index);
            var nameProp   = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Name));
            var albedoProp = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Albedo));
            var normalProp = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Normal));
            var maskProp   = element.FindPropertyRelative(nameof(TerrainTextureSetDesc.Mask));

            var  r            = rect;
            Rect namePropRect = r;
            namePropRect.width  = 260f;
            namePropRect.height = EditorGUIUtility.singleLineHeight;

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 89f;

            EditorGUI.BeginChangeCheck();
            EditorGUI.DelayedTextField(namePropRect, nameProp);
            if (EditorGUI.EndChangeCheck())
            {
                SetName(nameProp, albedoProp);
            }

            EditorGUIUtility.labelWidth =  labelWidth;
            r.y                         += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            r.height                    -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            Rect objectFieldRect = r;
            objectFieldRect.width  = 80f;
            objectFieldRect.height = 80f;

            Rect objectFieldLabelRect = r;
            objectFieldLabelRect.y      += 80f;
            objectFieldLabelRect.width  =  80f;
            objectFieldLabelRect.height =  EditorGUIUtility.singleLineHeight;
            r.width                     -= 90f;

            EditorGUI.BeginChangeCheck();
            albedoProp.objectReferenceValue =
                EditorGUI.ObjectField(objectFieldRect, albedoProp.objectReferenceValue, typeof(Texture2D), false);

            if (EditorGUI.EndChangeCheck())
                SetName(nameProp, albedoProp);

            EditorGUI.LabelField(objectFieldLabelRect, "Albedo");


            objectFieldRect.x      += 90f;
            objectFieldLabelRect.x += 90f;
            r.width                -= 90f;
            normalProp.objectReferenceValue =
                EditorGUI.ObjectField(objectFieldRect, normalProp.objectReferenceValue, typeof(Texture2D), false);
            EditorGUI.LabelField(objectFieldLabelRect, "Normal");

            objectFieldRect.x      += 90f;
            objectFieldLabelRect.x += 90f;
            r.width                -= 90f;
            r.x                    =  objectFieldRect.x + 90f;

            maskProp.objectReferenceValue =
                EditorGUI.ObjectField(objectFieldRect, maskProp.objectReferenceValue, typeof(Texture2D), false);
            EditorGUI.LabelField(objectFieldLabelRect, "Mask");

            var importer = (TerrainTextureSetCollectionImporter)this.target;

            List<Texture2D> wrongAlbedoTextures = new();
            List<Texture2D> wrongMaskTextures   = new();
            List<Texture2D> wrongNormalTextures = new();
            {
                CollectTexturesToReformat(albedoProp.objectReferenceValue,
                                          wrongAlbedoTextures,
                                          (int)importer.AlbedoResolution,
                                          (TextureFormat)importer.AlbedoFormat,
                                          true);
            }
            {
                CollectTexturesToReformat(normalProp.objectReferenceValue,
                                          wrongNormalTextures,
                                          (int)importer.NormalResolution,
                                          (TextureFormat)importer.NormalFormat,
                                          false);
            }
            {
                CollectTexturesToReformat(maskProp.objectReferenceValue,
                                          wrongMaskTextures,
                                          (int)importer.MaskResolution,
                                          (TextureFormat)importer.MaskFormat,
                                          false);
            }

            Rect additionalPropertiesRect = r;
            if (wrongAlbedoTextures.Count > 0 || wrongNormalTextures.Count > 0 || wrongMaskTextures.Count > 0)
            {
                report.Clear();

                for (int i = 0; i < wrongAlbedoTextures.Count; i++)
                {
                    report.AppendLine(wrongAlbedoTextures[i].name + $" is using wrong format ({wrongAlbedoTextures[i].format})");
                }

                for (int i = 0; i < wrongNormalTextures.Count; i++)
                {
                    report.AppendLine(wrongNormalTextures[i].name + $" is using wrong format ({wrongNormalTextures[i].format})");
                }

                for (int i = 0; i < wrongMaskTextures.Count; i++)
                {
                    report.AppendLine(wrongMaskTextures[i].name + $" is using wrong format ({wrongMaskTextures[i].format})");
                }

                MessageType messageType = wrongNormalTextures.Count == 0 ? MessageType.Warning : MessageType.Error;
                Texture     helpIcon    = getHelpIconsMethod.Invoke(null, new object[] { messageType }) as Texture;
                GUIContent warningContent = new("Some textures need to be reimported", helpIcon,
                                                report.ToString());

                additionalPropertiesRect.height = EditorGUIUtility.singleLineHeight * 2 + 5f;
                GUI.Label(additionalPropertiesRect, warningContent, EditorStyles.helpBox);

                Rect tooltipRect = additionalPropertiesRect;

                Rect fixButtonRect = additionalPropertiesRect;
                fixButtonRect.width  =  50f;
                fixButtonRect.height =  20f;
                fixButtonRect.x      += additionalPropertiesRect.width - 60f;
                fixButtonRect.y      += additionalPropertiesRect.height * 0.5f - (fixButtonRect.height * 0.5f);

                if (GUI.Button(fixButtonRect, "Fix"))
                {
                    FixTextures(wrongAlbedoTextures, (int)importer.AlbedoResolution, (TextureFormat)importer.AlbedoFormat, true);
                    FixTextures(wrongNormalTextures, (int)importer.NormalResolution, (TextureFormat)importer.NormalFormat, false);
                    FixTextures(wrongMaskTextures,   (int)importer.MaskResolution,   (TextureFormat)importer.MaskFormat,   false);

                    this.hasUnsavedChanges = true;
                }
            }
        };

        void SetName(SerializedProperty nameProp, SerializedProperty albedoProp)
        {
            if (nameProp.stringValue.IsNullOrWhiteSpace())
            {
                var tex = albedoProp.objectReferenceValue;
                if (tex)
                {
                    nameProp.stringValue = tex.name;
                }
            }
        }
    }

    void FixTextures(List<Texture2D> textures, int requiredSize, TextureFormat requiredFormat, bool requiredSRGB)
    {
        foreach (var texture in textures)
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (texturePath.IsNullOrEmpty())
            {
                Debug.LogError("Couldn't fix import settings on texture: " + texture.name);
                continue;
            }

            var texImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!texImporter)
            {
                Debug.LogError("Couldn't get importer for texture " + texture.name);
                continue;
            }

            var textureSettings = texImporter.GetPlatformTextureSettings(EditorUserBuildSettings.activeBuildTarget.ToString());
            textureSettings.overridden = true;
            textureSettings.format     = (TextureImporterFormat)requiredFormat;
            // textureSettings.maxTextureSize = requiredSize;

            texImporter.sRGBTexture = requiredSRGB;

            texImporter.SetPlatformTextureSettings(textureSettings);
            texImporter.SaveAndReimport();
        }
    }

    [System.Flags]
    internal enum CopyCheckResult
    {
        Ok          = 0,
        WrongFormat = 1,
        WrongSize   = 2,
        WrongSRGB   = 4,
    }

    internal static CopyCheckResult CanBeCopiedToTextureArray(Texture2D texture, int arrayResolution, TextureFormat arrayFormat, bool isSRGB)
    {
        CopyCheckResult o = 0;
        if (texture.format != arrayFormat)
            o |= CopyCheckResult.WrongFormat;

        if (texture.width != arrayResolution ||
            texture.height != arrayResolution)
            o |= CopyCheckResult.WrongSize;

        if (texture.isDataSRGB != isSRGB)
            o |= CopyCheckResult.WrongSRGB;

        return o;
    }

    internal enum ReformatCheckResult
    {
        Ok,
        NonMainAsset,
        UnsupportedExtension,
    }

    void CollectTexturesToReformat(Object          layer,
                                   List<Texture2D> toReformatList,
                                   int             requiredSize,
                                   TextureFormat   requiredFormat,
                                   bool            requiredSRGB)
    {
        if (!layer || layer is not Texture2D tex)
            return;

        Texture2D texture = tex;
        if (!texture)
            return;

        var copyCheck = CanBeCopiedToTextureArray(texture, requiredSize, requiredFormat, requiredSRGB);
        if ((copyCheck & (CopyCheckResult.WrongFormat |
                          CopyCheckResult.WrongSRGB)) != 0)
        {
            toReformatList.Add(texture);
        }
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(AlbedoFormat);
        EditorGUILayout.PropertyField(AlbedoResolution);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(NormalFormat);
        EditorGUILayout.PropertyField(NormalResolution);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(MaskFormat);
        EditorGUILayout.PropertyField(MaskResolution);
        EditorGUILayout.Space();

        textureSetsList.DoLayoutList();
        this.ApplyRevertGUI();
    }
}
}