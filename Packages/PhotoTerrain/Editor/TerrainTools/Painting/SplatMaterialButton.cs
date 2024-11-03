using Hollow.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HollowEditor.TerrainSystem
{
// TODO: Burn this with fire
class SplatMaterialButton : BaseField<int>
{
    public SplatMaterialButton() : base(null, null)
    {
        AddToClassList("material-selector");

        Image img = new();
        img.AddToClassList("material-selector-preview");
        img.focusable = true;

        Add(img);

        RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button == 0)
            {
            }
            else if (evt.button == 1)
            {
            }
        });

        /*RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if(DragAndDrop.objectReferences[0] is not PhotoTerrainMaterial)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;

            img.EnableInClassList("material-selector-preview-dragging-on", true);
            evt.StopPropagation();
        });

        RegisterCallback<DragPerformEvent>(evt =>
        {
            if (DragAndDrop.objectReferences[0] is not PhotoTerrainMaterial mat)
            {
                return;
            }

            img.EnableInClassList("material-selector-preview-dragging-on", false);
           // value = mat;
        });*/

        RegisterCallback<DragLeaveEvent>(evt => { img.EnableInClassList("material-selector-preview-dragging-on", false); });

        this.RegisterValueChangedCallback(evt =>
        {
            var terrain = TerrainTools.GetMainTerrain();
            img.image = terrain.palette.materials[evt.newValue].albedo;
        });
    }
}
}