using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class ItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemCategory category;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public ItemCategory Category => category;

    public virtual bool CanStack => false;
    public virtual int MaxStackSize => 1;
    public virtual bool SupportsPlacement => false;
    public virtual bool SupportsBlockBreaking => false;
    public virtual bool SupportsAttackAnimation => false;
    public virtual TileBase PlacementTile => null;
    public virtual float BreakSpeedMultiplier => 1f;

    protected void InitializeRuntime(string id, string displayLabel, Sprite itemIcon, ItemCategory itemCategory)
    {
        itemId = id;
        displayName = displayLabel;
        icon = itemIcon;
        category = itemCategory;
        hideFlags = HideFlags.HideAndDontSave;
        name = $"{GetType().Name}_{id}";
    }

    public void UpdateIcon(Sprite itemIcon)
    {
        if (itemIcon != null){
            icon = itemIcon;
        }
    }
}
