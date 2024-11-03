using System;

namespace HollowEditor.TerrainSystem
{
public class TerrainToolAttribute : Attribute
{
    public TerrainToolAttribute(string displayName, int sortingOrder = 0)
    {
        ID           = displayName;
        DisplayName  = displayName;
        SortingOrder = sortingOrder;
        Tooltip      = displayName;
    }

    public TerrainToolAttribute(string id, string displayName, int sortingOrder = 0)
    {
        ID           = id;
        DisplayName  = displayName;
        SortingOrder = sortingOrder;
        Tooltip      = displayName;
    }

    public TerrainToolAttribute(string id, string displayName, string tooltip, int sortingOrder = 0)
    {
        ID           = id;
        DisplayName  = displayName;
        SortingOrder = sortingOrder;
        Tooltip      = tooltip;
    }

    public string ID           { get; }
    public string DisplayName  { get; }
    public string Tooltip      { get; }
    public int    SortingOrder { get; }
}
}