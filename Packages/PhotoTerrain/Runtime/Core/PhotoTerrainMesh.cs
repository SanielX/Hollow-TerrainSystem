using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Hollow.TerrainSystem
{
[System.Serializable]
public struct PhotoTerrainMeshDescriptor
{
    public int vertexStart;
    public int vertexCount;

    public int indexStart;
    public int indexCount;

    public float2 origin; // pivot
    public float2 size;
}

[System.Serializable]
public struct TerrainVertex
{
    public ushort x, y;

    public TerrainVertex(Vector3 v)
    {
        Assert.IsTrue(v.x <= 1 && v.z <= 1, "v.x <= 1 && v.z <= 1");

        x = (ushort)(v.x * ushort.MaxValue);
        y = (ushort)(v.z * ushort.MaxValue);
    }
}

[System.Serializable]
public struct TerrainIndex
{
    public byte x, y;

    public TerrainIndex(Vector3 v)
    {
        Assert.IsTrue(v.x <= 1 && v.z <= 1, "v.x <= 1 && v.z <= 1");

        x = (byte)(v.x * byte.MaxValue);
        y = (byte)(v.z * byte.MaxValue);
    }
}

/// <summary>
/// Scriptable object that can hold mesh data usable by photo terrain system
/// </summary>
[CreateAssetMenu]
[PreferBinarySerialization]
public class PhotoTerrainMesh : ScriptableObject
{
    private GraphicsBuffer indexBuffer, fatIndexBuffer;
    private GraphicsBuffer vertexBuffer;

    [SerializeField] internal int                 generationHash;
    [SerializeField] string[]                     submeshNames;
    [SerializeField] PhotoTerrainMeshDescriptor[] submeshDescriptors;
    [SerializeField, NonReorderable] int[]        vertices; // Packed as { ushort x, y; }
    [SerializeField, NonReorderable] ushort[]     indices;

    void OnDisable()
    {
        if (indexBuffer is not null  && indexBuffer.IsValid())
            indexBuffer.Dispose();

        if (fatIndexBuffer is not null && fatIndexBuffer.IsValid())
            fatIndexBuffer.Dispose();

        if (vertexBuffer is not null && vertexBuffer.IsValid())
            vertexBuffer.Dispose();
    }

    public int SubmeshCount
    {
        get => submeshDescriptors?.Length ?? 0;
        set
        {
            if (value < 0 || value > short.MaxValue)
                throw new System.ArgumentException();

            Array.Resize(ref submeshDescriptors, value);
            Array.Resize(ref submeshNames,       value);
        }
    }

    public void SetSubmesh(int index, string name, PhotoTerrainMeshDescriptor descriptor)
    {
        submeshNames[index]       = name;
        submeshDescriptors[index] = descriptor;
    }

    public ref PhotoTerrainMeshDescriptor GetSubmesh(int index)
    {
        return ref submeshDescriptors[index];
    }

    public string GetSubmeshName(int index) => submeshNames[index];

    public int[] Vertices
    {
        get => vertices;
        set
        {
            this.vertices = value;

            if (vertexBuffer is not null && vertexBuffer.IsValid())
                vertexBuffer.Dispose();
        }
    }

    public ushort[] Indices
    {
        get => indices;
        set
        {
            this.indices = value;

            if (indexBuffer is not null && indexBuffer.IsValid())
                indexBuffer.Dispose();

            if (fatIndexBuffer is not null && fatIndexBuffer.IsValid())
                fatIndexBuffer.Dispose();
        }
    }

    public GraphicsBuffer FatIndexBuffer
    {
        get
        {
            if (fatIndexBuffer is not null && fatIndexBuffer.IsValid())
                return fatIndexBuffer;

            if (indices is not null && indices.Length > 0)
            {
                CreateFatIndexBuffer();
                return fatIndexBuffer;
            }

            return null;
        }
    }

    public GraphicsBuffer IndexBuffer
    {
        get
        {
            if (indexBuffer is not null && indexBuffer.IsValid())
                return indexBuffer;

            if (indices is not null && indices.Length > 0)
            {
                CreateIndexBuffer();
                return indexBuffer;
            }

            return null;
        }
    }

    public GraphicsBuffer VertexBuffer
    {
        get
        {
            if (vertexBuffer is not null && vertexBuffer.IsValid())
                return vertexBuffer;

            if (vertices is not null && vertices.Length > 0)
            {
                CreateVertexBuffer();
                return vertexBuffer;
            }

            return null;
        }
    }

    void CreateFatIndexBuffer()
    {
        fatIndexBuffer?.Dispose();
        fatIndexBuffer      = new(GraphicsBuffer.Target.Structured, indices.Length, sizeof(int));
        fatIndexBuffer.name = $"'{name}' Fat Index Buffer";

        NativeArray<uint> convertedIndices = new(this.indices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < convertedIndices.Length; i++)
        {
            convertedIndices[i] = indices[i];
        }


        fatIndexBuffer.SetData(convertedIndices);
        convertedIndices.Dispose();
    }

    void CreateIndexBuffer()
    {
        indexBuffer?.Dispose();
        indexBuffer      = new(GraphicsBuffer.Target.Index, indices.Length, sizeof(ushort));
        indexBuffer.name = $"'{name}' Index Buffer";
        indexBuffer.SetData(indices);
    }

    void CreateVertexBuffer()
    {
        vertexBuffer?.Dispose();
        vertexBuffer      = new(GraphicsBuffer.Target.Structured, vertices.Length, sizeof(int));
        vertexBuffer.name = $"'{name}' Vertices Buffer";
        vertexBuffer.SetData(vertices);
    }

    [ContextMenu("Gen Plane")]
    void GenPlane()
    {
        const int resolution = 16 + 1;
        GenerateUnitPlaneMeshJob job = new();
        job.Init(resolution, Allocator.TempJob);
        job.RunByRef();

        Vertices     = job.OutputVertices.Reinterpret<int>().ToArray();
        Indices      = job.OutputIndices.ToArray();
        SubmeshCount = 1;
        SetSubmesh(0, $"Plane x{resolution}", new()
        {
            vertexCount = Vertices.Length,
            indexCount  = Indices.Length,
            size        = 1,
        });

        job.Dispose();
    }
}
}