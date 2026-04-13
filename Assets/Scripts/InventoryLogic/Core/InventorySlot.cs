using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class InventorySlot
{
    [SerializeField] private ItemDefinition item;

    public int count;

    public ItemDefinition Item => item;
    public string ItemId => item != null ? item.ItemId : string.Empty;
    public string DisplayName => item != null ? item.DisplayName : string.Empty;
    public Sprite Icon => item != null ? item.Icon : null;
    public TileBase PlacementTile => item != null ? item.PlacementTile : null;
    public ItemCategory Category => item != null ? item.Category : ItemCategory.None;
    public bool IsEmpty => item == null || count <= 0;
    public bool IsFist => !IsEmpty && Category == ItemCategory.Fist;
    public bool IsTool => !IsEmpty && Category == ItemCategory.Tool;
    public bool IsPlaceableBlock => !IsEmpty && item.SupportsPlacement && PlacementTile != null;
    public bool CanBreakBlocks => !IsEmpty && item.SupportsBlockBreaking;
    public bool CanUseAttackAnimation => !IsEmpty && item.SupportsAttackAnimation;
    public bool CanStack => item != null && item.CanStack;
    public int MaxStackSize => item != null ? item.MaxStackSize : 0;
    public float BreakSpeedMultiplier => item != null ? item.BreakSpeedMultiplier : 1f;

    public void SetItem(ItemDefinition itemDefinition, int itemCount)
    {
        item = itemDefinition;
        count = itemCount;

        if (count <= 0 || item == null){
            Clear();
        }
    }

    public void Clear()
    {
        item = null;
        count = 0;
    }

    public bool CanStackWith(ItemDefinition otherItem)
    {
        return !IsEmpty && CanStack && item == otherItem;
    }
}
