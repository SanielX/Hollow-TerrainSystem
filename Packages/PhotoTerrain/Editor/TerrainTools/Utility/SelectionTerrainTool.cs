using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hollow;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using Unity.Collections;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HollowEditor.TerrainSystem.Utility
{
public struct TerrainRegionClipContents
{
    public TerrainRegionClipContents(Vector2 mi, Vector2 ma)
    {
        min = mi;
        max = ma;
        textures = null;
    }

    public Vector2     min;
    public Vector2     max;
    public Texture2D[] textures;
}

[TerrainTool("Rectangle Select")]
public class SelectionTerrainTool : TerrainTool
{
    public override Texture2D GetIcon() => EditorGUIUtility.IconContent("Outline Icon").image as Texture2D;

    enum State
    {
        WaitingForMouse,
        SelectingSize,
    }

    private State state;

    private Vector3 beginPosition;
    private Vector3 endPosition;

    internal Vector2 rectangleSize;

    private const int    magic_number     = 0xC0_CA_CE;
    private const string clipboard_header = "_PHOTO_TERRAIN_CLIPBOARD_HACK";

    SerializationStream SerializeRegionData(TerrainRegionClipContents contents)
    {
        var textures = contents.textures;

        int totalSize = 128;
        for (int i = 0; i < textures.Length; i++)
        {
            totalSize += textures[i].GetRawTextureData<byte>().Length + 256; // 256 bytes for name + aux data
        }

        SerializationStream stream = new(totalSize, Allocator.Persistent);

        Span<int> offsetPositions = stackalloc int[textures.Length];

        stream.WriteBytes(magic_number);
        stream.WriteBytes(contents.min);
        stream.WriteBytes(contents.max);

        stream.WriteBytes(textures.Length);
        for (int i = 0; i < textures.Length; i++)
        {
            var tex = textures[i];

            UnityEngine.Assertions.Assert.IsTrue(tex.name.Length < 128, "Texture name can't have more than 128 characters");

            offsetPositions[i] = stream.Position;
            stream.WriteBytes(-1); // This is where texture data will be placed later
            stream.WriteBytes(tex.name);
            stream.WriteBytes(tex.width);
            stream.WriteBytes(tex.height);
            stream.WriteBytes(tex.graphicsFormat);
            stream.WriteBytes(tex.GetRawTextureData<byte>().Length);
        }

        for (int i = 0; i < textures.Length; i++)
        {
            var tex = textures[i];

            int pos = stream.Position;
            stream.Position = offsetPositions[i];
            stream.WriteBytes(pos);
            stream.Position = pos;

            stream.WriteBytes((ReadOnlySpan<byte>)tex.GetRawTextureData<byte>().AsSpan());
        }

        return stream;
    }

    TerrainRegionClipContents DeserializeClipboard(NativeArray<byte> bytes, int length)
    {
        SerializationStream stream = new(bytes, Allocator.Temp);
        stream.IsReading = true;

        TerrainRegionClipContents contents = default;

        stream.ReadBytes(out int magic_number_check);
        UnityEngine.Assertions.Assert.IsTrue(magic_number_check == magic_number);

        stream.ReadBytes(out contents.min);
        stream.ReadBytes(out contents.max);
        stream.ReadBytes(out int textureCount);
        contents.textures = new Texture2D[textureCount];
        for (int i = 0; i < textureCount; i++)
        {
            stream.ReadBytes(out int bytesPosition);
            stream.ReadBytes(out string textureName);
            stream.ReadBytes(out int width);
            stream.ReadBytes(out int height);
            stream.ReadBytes(out GraphicsFormat format);
            stream.ReadBytes(out int bytesLength);

            int oldPos = stream.Position;
            stream.Position = bytesPosition;

            stream.ReadBytes(out ReadOnlySpan<byte> textureDataBytes);
            UnityEngine.Assertions.Assert.IsTrue(textureDataBytes.Length == bytesLength);

            Texture2D texture = new(width, height, format, TextureCreationFlags.DontInitializePixels) { name = textureName };
            var textureData = texture.GetRawTextureData<byte>().AsSpan();
            textureDataBytes.CopyTo(textureData);

            texture.Apply();

            contents.textures[i] = texture;

            stream.Position = oldPos;
        }

        stream.Dispose();

        return contents;
    }

    public override unsafe void OnToolGUI(EditorWindow window)
    {
        var evt = Event.current;

        if (rectangleSize != Vector2.zero && evt.type == EventType.ExecuteCommand)
        {
            // "Copy", "Cut", "Paste", "Delete", "SoftDelete", "Duplicate", "FrameSelected", "FrameSelectedWithLock", "SelectAll", "Find" and "FocusProjectWindow".
            if (evt.commandName == "Copy")
            {
                Span<Texture2D> spn = new[] { Texture2D.whiteTexture, Texture2D.blackTexture };

                RenderDoc.BeginCaptureRenderDoc(EditorWindow.focusedWindow);
                var terrains = TerrainTools.GetSelectedTerrains();
                var   targetTex = terrains[0].baseLayer.heightMap.GetLastTexture();
                var   size      = terrains[0].Size;
                float spacing   = size.x / (targetTex.width - 1); // *---*---*

                int resX = Mathf.CeilToInt(Mathf.Abs(rectangleSize.x) / spacing); // var worstCaseBrushResolution = Mathf.CeilToInt(worstCaseBrushSize / spacing);
                int resY = Mathf.CeilToInt(Mathf.Abs(rectangleSize.y) / spacing);

                RenderTextureDescriptor desc = new(resX, resY, targetTex.graphicsFormat, GraphicsFormat.None);
                RenderTexture rt = new(desc);

                var rectangleMin = Vector3.Min(beginPosition, endPosition) / spacing;
                rectangleMin.x = Mathf.Floor(rectangleMin.x);
                rectangleMin.y = Mathf.Floor(rectangleMin.y);
                rectangleMin.z = Mathf.Floor(rectangleMin.z);

                rectangleMin = rectangleMin * spacing;
                var rectangleMax = rectangleMin + new Vector3(spacing * (resX - 1), 0, spacing * (resY - 1));

                rectangleMin.y = 0.0f;
                rectangleMax.y = terrains[0].MaxHeight;

                // var view = PaintingContext.ComputeViewMatrixForAABB(rectangleMin, rectangleMax);
                // var proj = PaintingContext.ComputeProjMatrixForAABB(rectangleMax - rectangleMin);
                RenderUtility.ComputeViewProjMatricesForAABB(rectangleMin, rectangleMax, out var view, out var proj);

                var mat = TerrainResources.Instance.BrushRegionCopyMaterial;
                CommandBuffer cmd = new();
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(RTClearFlags.All, Color.clear, 1.0f, 0);
                cmd.SetViewProjectionMatrices(view, proj);
                cmd.SetGlobalFloat("UseUnityVPMatrix", 1.0f);

                for (int i = 0; i < terrains.Length; i++)
                {
                    var terrain      = terrains[i];
                    var regionTex    = terrains[i].baseLayer.heightMap.GetLastTexture();
                    var regionOrigin = terrains[i].transform.position.XZ();
                    var regionSize   = terrains[i].Size.XZ();

                    var regionMin = regionOrigin - new Vector2(spacing * 0.5f,                spacing * 0.5f);
                    var regionMax = (regionOrigin + regionSize) + new Vector2(spacing * 0.5f, spacing * 0.5f);

                    var bregionCenter = (regionMin + regionMax) / 2.0f;
                    var bregionSize   = (regionMax - regionMin);

                    cmd.SetGlobalVector("TargetTileBounds", new Vector4(bregionCenter.x, bregionCenter.y, bregionSize.x, bregionSize.y));
                    cmd.SetGlobalTexture("TargetTile", regionTex);

                    cmd.DrawMeshInstancedProcedural(ProceduralMesh.HorizontalQuad, 0, mat, 0, 1);
                }

                Graphics.ExecuteCommandBuffer(cmd);

                Texture2D tex = TextureUtility.CreateTexture2DFromRT(rt);
                tex.name = "heightmap";

                TerrainRegionClipContents contents = new(rectangleMin.XZ(), rectangleMax.XZ());
                contents.textures = new[] { tex };
                using var stream = SerializeRegionData(contents);

                StringBuilder builder = new(stream.Length * 2 + clipboard_header.Length * 2);
                builder.Append(clipboard_header);
                builder.Append(Convert.ToBase64String(stream.AsReadOnlyByteSpan()));

                GUIUtility.systemCopyBuffer = builder.ToString();

                ObjectUtility.SafeDestroy(ref rt);
                ObjectUtility.SafeDestroy(ref tex);
                RenderDoc.EndCaptureRenderDoc(EditorWindow.focusedWindow);

                evt.Use();
            }
            else if (evt.commandName == "Paste")
            {
                var copyBuffer = GUIUtility.systemCopyBuffer.AsSpan();
                if (copyBuffer.StartsWith(clipboard_header, StringComparison.Ordinal))
                {
                    copyBuffer = copyBuffer[clipboard_header.Length..];

                    NativeArray<byte> bytes = new(copyBuffer.Length, Allocator.Persistent);
                    Convert.TryFromBase64Chars(copyBuffer, bytes.AsSpan(), out int bytesWritten);

                    var contents = DeserializeClipboard(bytes, bytesWritten);
                    Debug.Log(contents.min + ":" + contents.max);
                    var tex = contents.textures[0];

                    Type t    = Type.GetType("UnityEditor.PropertyEditor,UnityEditor.CoreModule");
                    var  open = t?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).First(m => m.Name == "OpenPropertyEditor" && m.GetParameters().Length == 2); //, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    open?.Invoke(null, new object[] { tex, true });
                }

                evt.Use();
            }
        }

        if (rectangleSize != Vector2.zero)
        {
            Vector3[] rectanglePositions = new[]
            {
                Vector3.up * 0.12f + beginPosition,
                Vector3.up * 0.12f + beginPosition + Vector3.forward * rectangleSize.y,
                Vector3.up * 0.12f + beginPosition + Vector3.right   * rectangleSize.x + Vector3.forward * rectangleSize.y,
                Vector3.up * 0.12f + beginPosition + Vector3.right   * rectangleSize.x,
            };

            var center    = beginPosition + (rectangleSize.X0Z() * 0.5f);

            Matrix4x4 sliderMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(rectangleSize.x, 1, rectangleSize.y));
            Matrix4x4 sliderMatrixInv = sliderMatrix.inverse;
            var       prevMat      = Handles.matrix;
            Handles.matrix = sliderMatrix;

            var localCenter = sliderMatrixInv.MultiplyPoint3x4(center);
            var newCenter   = Handles.Slider2D(localCenter, Vector3.up, Vector3.right, Vector3.forward, .5f, Handles.RectangleHandleCap, Vector2.zero);
            var move        = sliderMatrix.MultiplyVector(newCenter - localCenter);

            Handles.matrix = Matrix4x4.identity;

            beginPosition += move;
            endPosition += move;

            Handles.zTest = CompareFunction.LessEqual;
            // Handles.DrawSolidRectangleWithOutline(rectanglePositions, Color.white.WithAlpha(0.12f), Color.clear);
            const float d = 7f;
            Handles.DrawDottedLine(rectanglePositions[0], rectanglePositions[1], d);
            Handles.DrawDottedLine(rectanglePositions[1], rectanglePositions[2], d);
            Handles.DrawDottedLine(rectanglePositions[2], rectanglePositions[3], d);
            Handles.DrawDottedLine(rectanglePositions[3], rectanglePositions[0], d);

            Handles.zTest = CompareFunction.Greater;
            Handles.color = Color.white.WithAlpha(0.2f);
            Handles.DrawDottedLine(rectanglePositions[0], rectanglePositions[1], d);
            Handles.DrawDottedLine(rectanglePositions[1], rectanglePositions[2], d);
            Handles.DrawDottedLine(rectanglePositions[2], rectanglePositions[3], d);
            Handles.DrawDottedLine(rectanglePositions[3], rectanglePositions[0], d);
            Handles.DrawSolidRectangleWithOutline(rectanglePositions, Color.white.WithAlpha(0.02f), Color.clear);
        }

        switch (state)
        {
        case State.WaitingForMouse:
        {
            if (evt.button == 0 && evt.type == EventType.MouseDown)
            {
                var success = PhotoTerrainRenderer.TerrainScenePlace(evt.mousePosition, out var position, out _);
                if (success)
                {
                    endPosition = beginPosition = position;
                    state = State.SelectingSize;
                }

                evt.Use();
            }

            break;
        }
        case State.SelectingSize:
        {
            window.Repaint();

            Plane p = new(Vector3.up, beginPosition);

            if (evt.type == EventType.MouseDrag)
            {
                var guiPosition = evt.mousePosition;
                guiPosition.y = Camera.current.pixelHeight - guiPosition.y;
                var ray       = Camera.current.ScreenPointToRay(guiPosition);

                bool success = p.Raycast(ray, out float enter);
                if (success)
                {
                    endPosition = ray.origin + ray.direction.normalized * enter;
                }
            }
            else if (evt.type == EventType.MouseUp)
            {
                state = State.WaitingForMouse;
            }

            {
                Vector3 rectangleDirection = endPosition - beginPosition;
                rectangleSize.x = rectangleDirection.x;
                rectangleSize.y = rectangleDirection.z;

                if (!evt.shift)
                {
                    float maxComponent = Mathf.Max(Mathf.Abs(rectangleSize.x), Mathf.Abs(rectangleSize.y));
                    rectangleSize = new(maxComponent * Mathf.Sign(rectangleSize.x), maxComponent * Mathf.Sign(rectangleSize.y));
                }
            }
            break;
        }
        default:
            throw new ArgumentOutOfRangeException();
        }
    }
}
}