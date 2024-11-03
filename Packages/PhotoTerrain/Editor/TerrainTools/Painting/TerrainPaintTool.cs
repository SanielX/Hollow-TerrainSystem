using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Hollow;
using Hollow.Extensions;
using Hollow.TerrainSystem;
using Unity.Mathematics;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace HollowEditor.TerrainSystem
{
public struct PaintingToolParameters
{
    public enum BrushPaintingMode
    {
        Generic,
        Height,
        Splat,
    }

    public static readonly PaintingToolParameters Default = new()
    {
        opacityExponent = 1f,
        opacityRange = new(0, 1)
    };

    public static readonly PaintingToolParameters DefaultHeight = new()
    {
        mode     = BrushPaintingMode.Height,
        opacityExponent = 1f / 8f,
        opacityRange    = new(1f / (short.MaxValue), 10f)
    };

    public BrushPaintingMode mode;
    public float   opacityExponent;
    public Vector2 opacityRange;

    public bool displayHeightTargetSlider;
}

[System.Serializable]
public abstract unsafe class TerrainPaintTool : TerrainTool
{
    public enum ToolState
    {
        Normal,
        ChangingParams
    }

    public enum ParamChange
    {
        Unknown,
        Size,
        Weight,
        Rotate,
    }

    [SerializeField, HideInInspector] string brushPresetJson;

    public PaintingToolParameters paintToolParameters = PaintingToolParameters.Default;

    private TerrainBrushPresetGPU[] presetGPUData;
    public  GraphicsBuffer          pickingResultBuffer;
    public  GraphicsBuffer          brushStateBuffer;
    public  GraphicsBuffer          brushIndirectArgsBuffer;
    public  GraphicsBuffer          brushPresetContstantBuffer;
    public  GraphicsBuffer          terrainTextureRegionsBuffer;

    protected Quaternion nextBrushRotation;

    protected ToolState   state;
    protected ParamChange stateTargetParameter;
    protected Vector2     capturedMouseGUIPosition;
    protected Vector3     capturedBrushPosition;
    protected Vector3     capturedBrushNormal;

    protected float toolChangeRadius;
    protected float sizeChangeSensitivity;
    protected float opacityChangeSensitivity;

    protected          CommandBuffer      cmd;
    protected          bool               isPainting;
    protected          TerrainBrushPreset brushPreset;

    public TerrainBrushPreset BrushPreset => brushPreset;

    protected virtual void OnEnable()
    {
        state             = ToolState.Normal;
        nextBrushRotation = Quaternion.identity;

        brushStateBuffer = new(GraphicsBuffer.Target.Structured, 256, sizeof(BrushGPUState));
        brushIndirectArgsBuffer = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.IndirectArguments, 256,
                                      sizeof(GraphicsBuffer.IndirectDrawIndexedArgs));
        brushPresetContstantBuffer = new(GraphicsBuffer.Target.Constant, 1, sizeof(TerrainBrushPresetGPU));
        pickingResultBuffer        = PhotoTerrainRenderer.CreatePickingResultsBuffer();

        GraphicsBuffer.IndirectDrawIndexedArgs[] setupArgs  = new GraphicsBuffer.IndirectDrawIndexedArgs[256];
        uint                                     indexCount = ProceduralMesh.HorizontalQuad.GetIndexCount(0);
        for (var i = 0; i < setupArgs.Length; i++)
        {
            setupArgs[i].indexCountPerInstance = indexCount; // Quad
        }

        brushIndirectArgsBuffer.SetData(setupArgs);

        terrainTextureRegionsBuffer = new(GraphicsBuffer.Target.Structured, 16, sizeof(float4));

        presetGPUData = new TerrainBrushPresetGPU[1];
    }

    protected virtual void OnDisable()
    {
        ObjectUtility.SafeDispose(ref brushStateBuffer);
        ObjectUtility.SafeDispose(ref pickingResultBuffer);
        ObjectUtility.SafeDispose(ref brushIndirectArgsBuffer);
        ObjectUtility.SafeDispose(ref brushPresetContstantBuffer);
        ObjectUtility.SafeDispose(ref terrainTextureRegionsBuffer);
    }

    public override void OnToolGUI(EditorWindow window)
    {
        // PhotoTerrainWorld.DisplayDebugData(TerrainTools.SelectedLayer);

        if (BrushPreset.jitter == 0f)
        {
            nextBrushRotation = Quaternion.identity;
        }

        if (cmd is null)
        {
            cmd = new();
        }

        int controlId = GUIUtility.GetControlID(97533366, FocusType.Passive);

        var evt     = Event.current;
        var evtType = evt.type;

        // Repaint any time mouse moves or *something* happens
        if (evt.isMouse || evt.isKey)
            window.Repaint();

        if (evtType == EventType.Repaint)
        {
            //    if(evt.shift) RenderDoc.BeginCaptureRenderDoc(SceneView.currentDrawingSceneView);

            cmd.Clear();

            DrawBrushGizmo(state);
            if (state == ToolState.Normal)
            {
                UpdateBrushStateGPU(cmd, SceneView.currentDrawingSceneView.camera, BrushPreset.Size, Quaternion.AngleAxis(BrushPreset.rotate, Vector3.up) * nextBrushRotation);
            }
            else if (state == ToolState.ChangingParams)
            {
                SetupBrush(cmd, capturedBrushPosition, Quaternion.AngleAxis(BrushPreset.rotate, Vector3.up) * nextBrushRotation, BrushPreset.Size);
            }

            Graphics.ExecuteCommandBuffer(cmd);

            var brushPreviewMaterial = TerrainResources.Instance.BrushPreviewMaterial;
            SetBrushProperties(brushPreviewMaterial);
            if (paintToolParameters.mode == PaintingToolParameters.BrushPaintingMode.Splat)
            {
                brushPreviewMaterial.SetPass(1);
            }
            else
            {
                brushPreviewMaterial.SetPass(0);
            }

            Graphics.DrawMeshNow(ProceduralMesh.Cube, default);

            //    if(evt.shift) RenderDoc.EndCaptureRenderDoc(SceneView.currentDrawingSceneView);
        }

        if (state == ToolState.ChangingParams)
        {
            if (!evt.alt || (evt.type == EventType.MouseUp && evt.button == 0))
            {
                GUIUtility.hotControl = 0;
                state = ToolState.Normal;
                SetCursorPosition((int)capturedMouseGUIPosition.x, (int)capturedMouseGUIPosition.y);
                return;
            }
            else
            {
                switch (Event.current.GetTypeForControl(controlId))
                {
                case EventType.MouseDrag:
                {
                    GUIUtility.hotControl = controlId;
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    if (stateTargetParameter == ParamChange.Unknown)
                    {
                        Vector2 currentMouseGUIPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                        Vector2 toCurrent               = (currentMouseGUIPosition - capturedMouseGUIPosition).normalized;
                        float   radius                  = (currentMouseGUIPosition - capturedMouseGUIPosition).magnitude;

                        if (radius > 4)
                        {
                            if (toCurrent.y > 0.5f || toCurrent.y < -0.5f)
                            {
                                stateTargetParameter = ParamChange.Weight;
                            }
                            else
                            {
                                stateTargetParameter = ParamChange.Size;
                            }
                        }
                    }
                    else if (stateTargetParameter == ParamChange.Size)
                    {
                        OnMouseChangeSize();
                        TerrainToolsGUI.RepaintAllInspectors();
                    }
                    else if (stateTargetParameter == ParamChange.Weight)
                    {
                        OnMouseChangeOpacity();
                        TerrainToolsGUI.RepaintAllInspectors();
                    }

                    evt.Use();

                    break;
                }
                }
            }
        }

        if (!isPainting)
        {
            if (evt.alt && evt.type == EventType.MouseDown)
            {
                var pickingResult = PickAtMousePosition(cmd, Camera.current);
                if (pickingResult.TerrainInstanceID != 0)
                {
                    sizeChangeSensitivity    = CalculateFloatDragSensitivity(BrushPreset.size);
                    opacityChangeSensitivity = CalculateFloatDragSensitivity(BrushPreset.opacity);

                    capturedMouseGUIPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    capturedBrushPosition    = pickingResult.Position;
                    capturedBrushNormal      = pickingResult.Normal;
                    state                    = ToolState.ChangingParams;
                    stateTargetParameter     = ParamChange.Unknown;
                    evt.Use();
                }
            }
            else if (evt.alt && evt.type == EventType.ScrollWheel)
            {
                BrushPreset.rotate = Mathf.Repeat(BrushPreset.rotate + evt.delta.y * 1.5f, 360);
                evt.Use();
                TerrainToolsGUI.RepaintAllInspectors();
            }
        }

        if (!evt.isMouse || evt.button != 0)
        {
            return;
        }

        if (!isPainting && evtType == EventType.MouseDown)
        {
            if (evt.control)
            {
                if (OnPicking(evt.shift))
                {
                    evt.Use();
                    return;
                }
            }

            evt.Use(); // Even if invalid, use anyway, loosing your selection if misclicked is annoying
            isPainting = true;
        }
        else if (isPainting && (evtType == EventType.MouseUp || evtType == EventType.MouseLeaveWindow))
        {
            isPainting = false;
        }

        if (!isPainting)
            return;

        if (evtType == EventType.MouseDrag || evtType == EventType.MouseDown)
        {
            bool rd = TerrainTools.GetMainTerrain()._renderDocBrush;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (rd)
                RenderDoc.BeginCaptureRenderDoc(SceneView.currentDrawingSceneView);
            cmd.Clear();

            cmd.BeginSample("Picking");

            // Step 1: Upload brush size, rotation, texture etc
            UploadBrushPresetParameters(cmd, BrushPreset);
            UpdateBrushStateGPU(cmd, SceneView.currentDrawingSceneView.camera, BrushPreset.Size, Quaternion.AngleAxis(BrushPreset.rotate, Vector3.up) * nextBrushRotation);

            cmd.EndSample("Picking");

            cmd.BeginSample("Blit");
            Blit();
            cmd.EndSample("Blit");

            Graphics.ExecuteCommandBuffer(cmd);

            if (rd)
                RenderDoc.EndCaptureRenderDoc(SceneView.currentDrawingSceneView);

            if (BrushPreset.jitter > 0.0f)
                nextBrushRotation = Quaternion.AngleAxis(Random.Range(-BrushPreset.jitter, BrushPreset.jitter), Vector3.up).normalized;

            evt.Use();
        }
    }

    public enum ContextMode
    {
        Vertex,
        UV
    }

    public struct ContextArea
    {
        public RenderTargetIdentifier  rt;
        public RenderTextureDescriptor desc;
        public string resourceName;
        public float border;
    }

    protected ContextArea SetupContext(int index, string texName, ContextMode mode, int additionalPixels = 0)
    {
        // height, holes - per vertex, splat map - uv-space. Difference is center of pixels.
        // Vertex painting is along edges
        /*
                 VERTEX                       UV <- all pixels are inside
            *───*───*───*───*           ┌─────────────┐
            │               │           │*   *   *   *│
            *   *   *   *   *           │             │
            │               │           │*   *   *   *│
            *   *   *   *   *           │             │
            │               │           │*   *   *   *│
            *───*───*───*───*           └─────────────┘

         */
        bool isVert = mode == ContextMode.Vertex;

        // NOTE: ONLY height&hole maps needs all this bullshit with border based on spacing, splat maps are just normal textures that fit within the terrain itself
        // That's because heightmap is essentially vertex painting, so it's borders need to align with border of the mesh
        // Splat map doesn't care, neither should any textures for trees and whatnot
        var terrains  = TerrainTools.GetSelectedTerrains();
        var targetTex = terrains[0].baseLayer.GetTexture(texName).GetLastTexture();

        float   spacing         = isVert ? terrains[0].Size.x / (targetTex.width - 1) : terrains[0].Size.x / (targetTex.width); // *---*---*
        Vector2 contextAreaSize = new Vector2(BrushPreset.Size.x, BrushPreset.Size.y) * Mathf.Sqrt(2)
                                + new Vector2((spacing * additionalPixels), (spacing * additionalPixels)); // can be 45 deg rotated, so sqrt(2)
        int     contextAreaX    = Mathf.CeilToInt(contextAreaSize.x / spacing);
        int     contextAreaY    = Mathf.CeilToInt(contextAreaSize.y / spacing);
        float   border          = isVert ? spacing * 0.5f : 0;

        var shader = TerrainResources.Instance.BrushUpdateShader;
        cmd.SetComputeBufferParam(shader, 1, "BrushStateUAV", brushStateBuffer);

        cmd.SetComputeFloatParam (shader, "PixelSpacing",      spacing);
        cmd.SetComputeVectorParam(shader, "ContextSize",       contextAreaSize);
        cmd.SetComputeFloatParam (shader, "BrushRegionSize",   spacing * (contextAreaX - 1));
        cmd.SetComputeVectorParam(shader, "ContextResolution", new Vector2(contextAreaX, contextAreaY) - Vector2.one);
        cmd.SetComputeIntParam   (shader, "_TargetContextIndex", index);

        cmd.DispatchCompute(shader, 1, 1, 1, 1);

        RenderTextureDescriptor contextDesc = new(contextAreaX, contextAreaY, targetTex.graphicsFormat, GraphicsFormat.None) { enableRandomWrite = true };
        int contextAreaId = Shader.PropertyToID(texName + "_ContextArea");
        cmd.GetTemporaryRT(contextAreaId,  contextDesc);

        BlitTilesToContext(terrains, contextAreaId, texName, border);
        ContextArea result;
        result.rt           = contextAreaId;
        result.desc         = contextDesc;
        result.border       = border;
        result.resourceName = texName;

        return result;
    }

    protected void ApplyContext(in ContextArea ctx, bool undo = true)
    {
        BlitContextToTiles(TerrainTools.GetSelectedTerrains(), ctx.rt, ctx.resourceName, ctx.border);
    }

    protected virtual void Blit()
    {
    }

    internal void BlitTilesToContext(ReadOnlySpan<PhotoTerrain> terrains, RenderTargetIdentifier contextAreaId, string texName, float border)
    {
        cmd.BeginSample("Copy Tiles To Context");
        cmd.SetRenderTarget(contextAreaId);
        cmd.ClearRenderTarget(true, true, Color.clear);

        var copyMat = TerrainResources.Instance.BrushRegionCopyMaterial;
        for (int i = 0; i < terrains.Length; i++)
        {
            var regionTex    = terrains[i].baseLayer.GetTexture(texName).GetLastTexture();
            var regionOrigin = terrains[i].transform.position.XZ();
            var regionSize   = terrains[i].Size.XZ();

            var regionMin = regionOrigin                - new Vector2(border, border);
            var regionMax = (regionOrigin + regionSize) + new Vector2(border, border);

            var bregionCenter = (regionMin + regionMax) / 2.0f;
            var bregionSize   = (regionMax - regionMin);

            cmd.SetGlobalVector ("TargetTileBounds", new Vector4(bregionCenter.x, bregionCenter.y, bregionSize.x, bregionSize.y));
            cmd.SetGlobalTexture("TargetTile", regionTex);

            BlitAtMousePosition(cmd, copyMat);
        }

        cmd.EndSample("Copy Tiles To Context");
    }

    internal void BlitContextToTiles(ReadOnlySpan<PhotoTerrain> terrains, RenderTargetIdentifier contextAreaId, string texName, float border)
    {
        cmd.BeginSample("Copy To Tiles");

        cmd.SetGlobalFloat("UseUnityVPMatrix", 0.0f);
        var copyMat = TerrainResources.Instance.BrushRegionCopyMaterial;
        for (int i = 0; i < terrains.Length; i++)
        {
            var terrainTex = terrains[i].baseLayer.GetTexture(texName);

            terrainTex.RecordUndo();
            RenderTexture regionTex    = terrainTex.GetRenderTexture();
            var           regionOrigin = terrains[i].transform.position.XZ();
            var           regionSize   = terrains[i].Size.XZ();

            var     s         = new Vector2(border, border);
            Vector2 regionMin = regionOrigin - s;
            Vector2 regionMax = (regionOrigin + regionSize) + s;

            Vector3 min  = new Vector3(regionMin.x, 0,   regionMin.y);
            Vector3 max  = new Vector3(regionMax.x, 512, regionMax.y);

            RenderUtility.ComputeViewProjMatricesForAABB(min, max, out var view, out var proj);
            // var     view = PaintingState.ComputeViewMatrixForAABB(min, max);     
            // var bSize = max - min;
            // var proj  = PaintingState.ComputeProjMatrixForAABB(bSize);
            proj = GL.GetGPUProjectionMatrix(proj, true);

            var vp = math.mul(proj, view);
            cmd.SetGlobalMatrix("region_MatrixVP", vp);
            cmd.SetRenderTarget(regionTex);
            cmd.SetViewProjectionMatrices(view, proj);

            cmd.SetGlobalTexture("BrushStaging", contextAreaId);
            BlitAtMousePosition(cmd, copyMat, 1);
        }

        cmd.EndSample("Copy To Tiles");
    }

    internal static float CalculateFloatDragSensitivity(float value)
    {
        return float.IsInfinity(value) || float.IsNaN(value) ? 0.0f : Mathf.Max(1.0f, Mathf.Pow(Mathf.Abs(value), 0.5f)) * 0.029999999329447746f;
    }

    protected virtual void DrawBrushGizmo(ToolState toolState)
    {
        if (toolState == ToolState.ChangingParams)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(capturedBrushPosition, Vector3.up, Mathf.Max(BrushPreset.Size.x, BrushPreset.Size.y) * 0.5f);
        }
    }

    protected virtual void OnMouseChangeOpacity()
    {
        BrushPreset.opacity += -Event.current.delta.y * opacityChangeSensitivity * 0.1f;
    }

    protected virtual void OnMouseChangeSize()
    {
        BrushPreset.size += Event.current.delta.x * sizeChangeSensitivity;
    }

    protected virtual bool OnPicking(bool holdingShift)
    {
        return false;
    }

    protected internal override void SaveToolData()
    {
        if (brushPreset)
        {
            brushPresetJson = EditorJsonUtility.ToJson(brushPreset);
        }
        else
        {
            brushPresetJson = "";
        }

        base.SaveToolData();
    }

    protected internal override void LoadToolData()
    {
        base.LoadToolData();

        if (!brushPresetJson.IsNullOrEmpty())
        {
            brushPreset = ScriptableObject.CreateInstance<TerrainBrushPreset>();
            EditorJsonUtility.FromJsonOverwrite(brushPresetJson, brushPreset);
        }
        else
        {
            brushPreset = TerrainBrushPreset.CreateDefaultBrush();
        }

        brushPreset.hideFlags |= HideFlags.DontSave;
    }

    protected internal override VisualElement CreateHelpPopupContent()
    {
        VisualElement root = new();
        root.style.minWidth = 250f;

        Label controlsLabel = new("[LMB] to raise terrain\n" +
                                  "[LMB+Shift] to lower terrain");

        root.Add(controlsLabel);
        return root;
    }


    public virtual void CreateEarlyBrushContent(VisualElement root)
    {
    }

    public void UploadBrushPresetParameters(CommandBuffer commandBuffer, TerrainBrushPreset preset)
    {
        if (!preset)
            return;

        preset.WriteGPUData(ref presetGPUData[0], TranslateBrushOpacity(preset.opacity));
        presetGPUData[0].opacity *= Event.current.pressure;

        commandBuffer.SetBufferData(brushPresetContstantBuffer, presetGPUData);
        commandBuffer.SetGlobalConstantBuffer(brushPresetContstantBuffer, "PT_Brush_ContantBuffer", 0, brushPresetContstantBuffer.stride);
        commandBuffer.SetGlobalTexture("_PT_Brush_Mask", preset.Mask);
    }

    protected virtual float TranslateBrushOpacity(float opacity)
    {
        return opacity;
    }

    public PhotoTerrainRenderer.TerrainPickingResult PickAtMousePosition(CommandBuffer commandBuffer, Camera camera)
    {
        var mousePos = Event.current.mousePosition;
        mousePos.y = Camera.current.pixelHeight - Event.current.mousePosition.y;

        int visibleTerrainCount = PhotoTerrainRenderer.RenderTerrainsPicking(commandBuffer, camera, pickingResultBuffer, mousePos);
        if (visibleTerrainCount > 0)
        {
            return PhotoTerrainRenderer.ReadPickingResult(pickingResultBuffer);
        }
        else
        {
            return default;
        }
    }

    public bool SetupBrush(CommandBuffer _cmd, Vector3 position, Quaternion rotation, Vector2 brushSize)
    {
        Vector3 size = new(brushSize.x, 1, brushSize.y);
        Matrix4x4 brushMatrix = Matrix4x4.TRS(position, rotation, size);

        BrushGPUState[] brushGPUState = ArrayPool<BrushGPUState>.Shared.Rent(1);
        brushGPUState[0].objectToWorld = brushMatrix;
        brushGPUState[0].brushPosition = position;
        brushGPUState[0].brushSize     = size;
        brushGPUState[0].brushRotation = ((quaternion)rotation).value;
        brushGPUState[0].isValid       = 1;

        _cmd.SetBufferData(brushStateBuffer, brushGPUState, 0, 0, 1);

        _cmd.SetGlobalBuffer("BrushStateBuffer",   brushStateBuffer);

        ArrayPool<BrushGPUState>.Shared.Return(brushGPUState);

        return true;
    }

    /// <summary>
    /// Render terrain with picking shader, determine where the mouse lands, compute brush TRS matrix,
    /// bounds and put that in BrushStateBuffer globals
    /// </summary>
    public bool UpdateBrushStateGPU(CommandBuffer _cmd, Camera camera, Vector2 brushSize, Quaternion brushRotation)
    {
        var mousePos = Event.current.mousePosition;
        mousePos.y   = Camera.current.pixelHeight - Event.current.mousePosition.y;

        _cmd.BeginSample("Picking");

        int visibleTerrainCount = PhotoTerrainRenderer.RenderTerrainsPicking(_cmd, camera, pickingResultBuffer, mousePos, drawHoles: false);

        _cmd.EndSample("Picking");

        var brushUpdate = TerrainResources.Instance.BrushUpdateShader;

        _cmd.BeginSample("Brush Update");
        _cmd.SetComputeBufferParam(brushUpdate, 0, "PickingResult",        pickingResultBuffer);
        _cmd.SetComputeBufferParam(brushUpdate, 0, "BrushIndirectArgsUAV", brushIndirectArgsBuffer);
        _cmd.SetComputeBufferParam(brushUpdate, 0, "BrushStateUAV",        brushStateBuffer);
        _cmd.SetComputeVectorParam(brushUpdate, "BrushScale",    new Vector3(brushSize.x, 1, brushSize.y));
        _cmd.SetComputeVectorParam(brushUpdate, "BrushRotation", new(brushRotation.x, brushRotation.y, brushRotation.z, brushRotation.w));

        _cmd.DispatchCompute(brushUpdate, 0, 1, 1, 1);
        _cmd.SetGlobalBuffer("BrushStateBuffer",   brushStateBuffer);
        _cmd.EndSample("Brush Update");

        return true;
    }

    public void SetBrushProperties(Material mat)
    {
        mat.SetBuffer("BrushStateBuffer", brushStateBuffer);
    }

    public void BlitAtMousePosition(CommandBuffer cmd, Material mat, int shaderPass = 0, MaterialPropertyBlock block = null)
    {
        cmd.DrawMeshInstancedIndirect(ProceduralMesh.HorizontalQuad, 0, mat, shaderPass, brushIndirectArgsBuffer, 0, block);
    }

    private void SetCursorPosition(int a, int b)
    {
#if UNITY_EDITOR_WIN
        SetCursorPos(a, b);
#endif
    }

#if UNITY_EDITOR_WIN
    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int X, int Y);
#endif
}
}