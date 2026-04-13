using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class InventorySlot
{
    public string blockName;
    public TileBase blockTile;
    public int count;
    public Sprite icon;

    public bool IsEmpty => count <= 0 || string.IsNullOrEmpty(blockName);

    public void SetItem(string name, TileBase tile, Sprite itemIcon, int itemCount)
    {
        blockName = name;
        blockTile = tile;
        icon = itemIcon;
        count = itemCount;
    }

    public void Clear()
    {
        blockName = string.Empty;
        blockTile = null;
        icon = null;
        count = 0;
    }

    public bool CanStackWith(string name, TileBase tile)
    {
        return !IsEmpty && blockName == name && blockTile == tile;
    }
}
