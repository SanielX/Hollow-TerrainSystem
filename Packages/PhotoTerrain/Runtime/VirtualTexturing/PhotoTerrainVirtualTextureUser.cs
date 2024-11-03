using System;
using Hollow.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hollow.VirtualTexturing
{
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class PhotoTerrainVirtualTextureUser : MonoBehaviour
{
    public const int UAV_SLOT = 5;

    private Action<AsyncGPUReadbackRequest> onAsyncReadbackDone;
    private int2 lastUsedScreenSize;

    private RenderTexture    uav;
    private new Camera       camera;
    private NativeArray<int> readbackData;

    private int lastFrameUpdatedData;

    private CBuffer<VTFeedbackBufferCBuffer> commonDataBuffer;

    public NativeArray<int> LatestData      => readbackData;
    public int              LastUpdateFrame => lastFrameUpdatedData;

    void OnEnable()
    {
        onAsyncReadbackDone = OnReadbackComplete;
        commonDataBuffer = new();
        camera = GetComponent<Camera>();
    }

    void OnDisable()
    {
        ReleaseBuffers();
        commonDataBuffer.Dispose();
        if (readbackData.IsCreated)
            readbackData.Dispose();
    }

    public void Refresh(int w, int h)
    {
        if (!readbackData.IsCreated || !uav || lastUsedScreenSize.x != w || lastUsedScreenSize.y != h)
        {
            ReleaseBuffers();
            CreateBuffers(w, h);

            lastUsedScreenSize = new(w, h);
        }
    }

    public void BindFeedbackUAV(CommandBuffer cmd)
    {
        if (!uav)
        {
            Debug.LogError("Virtual texture feedback UAV was not created!", this);
            return;
        }

        commonDataBuffer.Update(cmd, new()
        {
            size  = lastUsedScreenSize,
            frame = (Time.frameCount & 63) // Defines which pixels are gonna be able to write into feedback buffer
        });

        commonDataBuffer.SetGlobal(cmd, Shader.PropertyToID("AVT_VirtualTextureFeedbackData"));

        cmd.SetRenderTarget(uav);
        cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 0, 0);
        // render doc is really unhappy that settings random write target creates overlapping rts
        cmd.SetRenderTarget(Graphics.activeColorBuffer);
        cmd.SetRandomWriteTarget(UAV_SLOT, uav);
    }

    public void RequestFeedbackUAVRead(CommandBuffer cmd)
    {
        if (!uav)
        {
            Debug.LogError("Virtual texture feedback UAV was not created!", this);
            return;
        }

        cmd.ClearRandomWriteTargets();
        cmd.RequestAsyncReadback(uav, onAsyncReadbackDone);
    }

    void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        if (!readbackData.IsCreated)
            return;

        lastFrameUpdatedData = Time.frameCount;

        var colors = request.GetData<int>(); // One pixel is 32 bits, so we treat it as uint
        if (colors.Length != readbackData.Length)
            return;

        // MortonEncodeTexturePixels encodeJob;
        // encodeJob.width  = uav.width;
        // encodeJob.input  = colors;
        // encodeJob.output = readbackData;
        // encodeJob.RunByRef();
        readbackData.CopyFrom(colors);
    }

    void CreateBuffers(int sizeX, int sizeY)
    {
        // Make actual readback buffer 8 times smaller than actual RTs
        sizeX >>= 3;
        sizeY >>= 3;

        uav                   = new RenderTexture(sizeX, sizeY, GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.None);
        uav.name              = camera.name + "_AVT_Feedback";
        uav.enableRandomWrite = true;
        uav.useMipMap         = false;
        uav.hideFlags |= HideFlags.DontSave;

        uav.Create();

        readbackData = new NativeArray<int>(sizeX * sizeY, Allocator.Persistent);
    }

    void ReleaseBuffers()
    {
        ObjectUtility.SafeDestroy(ref uav);

        if (readbackData.IsCreated)
            readbackData.Dispose();
    }
}
}