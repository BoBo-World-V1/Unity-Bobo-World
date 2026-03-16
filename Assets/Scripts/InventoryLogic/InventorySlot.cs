using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class InventorySlot
{
    public string blockName;
    public TileBase blockTile;
    public int count;
    public Sprite icon;
}