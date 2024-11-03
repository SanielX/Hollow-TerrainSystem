using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hollow.TerrainSystem
{
// TODO: This doesn't need to be its own class now that grid doesn't recreate when terrains change
public class PhotoTerrainWorldGrid
{
    public struct Cell
    {
        public short virtualImageId;
    }

    public PhotoTerrainWorldGrid()
    {
    }

    public float   cellSize;
    public int     xLength, yLength;
    public int4    gridCoordsRange; // xy - min, zw - max (exclusive)
    public float4  gridWorldRange;

    public Cell[] grid;

    public int XLength => xLength;
    public int YLength => yLength;

    public bool IsValid(int2 worldGridCoord)
    {
        int2 localCoord = worldGridCoord - gridCoordsRange.xy;

        return localCoord.x >= 0 && localCoord.y >= 0 && localCoord.x < xLength && localCoord.y < yLength;
    }

    public ref Cell GetCellFromGlobal(int2 worldGridCoord)
    {
        int index = WorldGridCoordToCellIndex(worldGridCoord);
        return ref grid[index];
    }

    public ref Cell GetCellFromLocal(int2 localGridCoord)
    {
        int index = LocalGridCoordToCellIndex(localGridCoord);
        return ref grid[index];
    }

    public void Create(float cellSize, int cellCount)
    {
        this.cellSize = cellSize;
        float size = cellSize * cellCount * 0.5f; // 12km, 6km to the left of center, 12 to the right
        UBounds bounds = new(-size, size);

        // We need to make sure grid always lies in multiples of cellSize
        // This way, when resizing, we can more easily populate it back
        gridCoordsRange.xy = (int2)math.floor(bounds.min.xz / cellSize);
        gridCoordsRange.zw = (int2)math.ceil (bounds.max.xz / cellSize);
        gridWorldRange.xy  = (float2) gridCoordsRange.xy  * cellSize;
        gridWorldRange.zw  = (float2)(gridCoordsRange.zw) * cellSize;

        xLength = gridCoordsRange.z - gridCoordsRange.x;
        yLength = gridCoordsRange.w - gridCoordsRange.y;

        grid = new Cell[xLength * yLength];
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = new() { virtualImageId = -1 };
        }
    }

    public int2 WorldCoordToWorldGridCoord(float2 worldPosition)
    {
        return (int2)math.floor(worldPosition / cellSize);
    }

    public int WorldGridCoordToCellIndex(int2 gridCoord)
    {
        var localCoord = WorldGridCoordToLocalGridCoord(gridCoord);
        return LocalGridCoordToCellIndex(localCoord);
    }

    int2 WorldGridCoordToLocalGridCoord(int2 gridCoord)
    {
        int2 localCoord = gridCoord - gridCoordsRange.xy;
        return localCoord;
    }

    public int LocalGridCoordToCellIndex(int2 localCoord)
    {
        int index = localCoord.y * XLength + localCoord.x;
        return index;
    }

    public int2 IndexToGridCoord(int index)
    {
        int x = index % xLength;
        int y = index / xLength;

        return new int2(x, y) + gridCoordsRange.xy;
    }
}
}