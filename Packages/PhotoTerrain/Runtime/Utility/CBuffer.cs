using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hollow.Rendering
{
public unsafe class CBuffer<T> : IDisposable where T : unmanaged
{
    public CBuffer(string name = "")
    {
        temp = new T[1];
        globals = new HashSet<int>();

        // const int minSize = 128; // Constant buffers that are too small tend to act funny
        // int itemCount = Mathf.Max(1, minSize / sizeof(T));
        buffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, sizeof(T));

#if UNITY_ASSERTIONS
        if (name is not (null or ""))
            buffer.name = name;

        StackTrace trace = new StackTrace(1, fNeedFileInfo: true);
        var allocFrame = trace.GetFrame(0);
        allocationContext = $"{allocFrame.GetFileName()}::{allocFrame.GetMethod()}, Line: {allocFrame.GetFileLineNumber()}";
#endif
    }

#if UNITY_ASSERTIONS
    string allocationContext;

    ~CBuffer()
    {
        Debug.LogError($"Constant buffer was not disposed manually.\nAllocated in: {allocationContext}");
        Dispose();
    }
#endif

    HashSet<int> globals;
    T[] temp;
    GraphicsBuffer buffer;

    /// <summary>
    /// Returns reference to internal array element which will be uploaded to CBuffer when using <see cref="Update()"/>
    /// </summary>
    public ref T GetDataForWriting() => ref temp[0];

    public GraphicsBuffer GraphicsBuffer => buffer;

    public void Update()
    {
        buffer.SetData(temp);
    }

    public void Update(CommandBuffer cmd)
    {
        cmd.SetBufferData(buffer, temp);
    }

    public void Update(in T data)
    {
        temp[0] = data;
        buffer.SetData(temp);
    }

    public void Update(CommandBuffer cmd, in T data)
    {
        temp[0] = data;
        cmd.SetBufferData(buffer, temp);
    }

    public void SetGlobal(CommandBuffer cmd, int id)
    {
        globals.Add(id);
        cmd.SetGlobalConstantBuffer(buffer, id, 0, buffer.stride * buffer.count);
    }

    public void SetGlobal(string id) => SetGlobal(Shader.PropertyToID(id));

    public void SetGlobal(int id)
    {
        globals.Add(id);
        Shader.SetGlobalConstantBuffer(id, buffer, 0, buffer.stride * buffer.count);
    }

    public void SetCompute(CommandBuffer cmd, ComputeShader computeShader, int kernel, int id)
    {
        cmd.SetComputeConstantBufferParam(computeShader, id, buffer, 0, buffer.stride * buffer.count);
    }

    public void Dispose()
    {
        foreach (var globalID in globals)
        {
            Shader.SetGlobalConstantBuffer(globalID, (GraphicsBuffer)null, 0, 0);
        }

        buffer.Release();
#if UNITY_ASSERTIONS
        GC.SuppressFinalize(this);
#endif
    }

    public void Release()
    {
        Dispose();
    }
}

internal static class ConstantBufferExt
{
    public static void SetGlobalCBuffer<T>(this CommandBuffer cmd, int id, CBuffer<T> buffer) where T : unmanaged
    {
        buffer.SetGlobal(cmd, id);
    }

    public static void SetComputeGlobalCBuffer<T>(this CommandBuffer cmd, ComputeShader cs, int kernelIndex, int id, CBuffer<T> buffer) where T : unmanaged
    {
        buffer.SetCompute(cmd, cs, kernelIndex, id);
    }
}
}