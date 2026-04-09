using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class InventorySlot
{
    // Stored item data for one inventory slot.
    public string blockName;
    public TileBase blockTile;
    public int count;
    public Sprite icon;
}