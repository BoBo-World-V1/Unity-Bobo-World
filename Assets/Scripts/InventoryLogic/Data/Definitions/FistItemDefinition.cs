using UnityEngine;

[CreateAssetMenu(fileName = "FistItem", menuName = "Game/Items/Fist Item")]
public class FistItemDefinition : ItemDefinition
{
    public override bool SupportsBlockBreaking => true;
    public override bool SupportsAttackAnimation => true;

    public void InitializeRuntime(Sprite itemIcon)
    {
        InitializeRuntime(RuntimeItemCatalog.FistItemId, "Fist", itemIcon, ItemCategory.Fist);
    }
}
