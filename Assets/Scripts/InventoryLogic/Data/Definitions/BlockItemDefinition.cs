using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "BlockItem", menuName = "Game/Items/Block Item")]
public class BlockItemDefinition : ItemDefinition
{
    [SerializeField] private TileBase placementTile;
    [SerializeField] private float breakTime = 0.5f;

    public override bool CanStack => true;
    public override int MaxStackSize => 999;
    public override bool SupportsPlacement => placementTile != null;
    public override TileBase PlacementTile => placementTile;
    public float BreakTime => breakTime;

    public void InitializeRuntime(string id, string itemName, Sprite itemIcon, TileBase tile, float blockBreakTime)
    {
        InitializeRuntime(id, itemName, itemIcon, ItemCategory.Block);
        placementTile = tile;
        breakTime = Mathf.Max(0.05f, blockBreakTime);
    }

    public void UpdateTile(TileBase tile, float blockBreakTime)
    {
        if (tile != null){
            placementTile = tile;
        }

        breakTime = Mathf.Max(0.05f, blockBreakTime);
    }
}
