using UnityEngine;

[CreateAssetMenu(fileName = "ToolItem", menuName = "Game/Items/Tool Item")]
public class ToolItemDefinition : ItemDefinition
{
    [SerializeField] private float breakSpeedMultiplier = 1f;
    [SerializeField] private bool supportsAttackAnimation = true;

    public override bool SupportsBlockBreaking => true;
    public override bool SupportsAttackAnimation => supportsAttackAnimation;
    public override float BreakSpeedMultiplier => Mathf.Max(1f, breakSpeedMultiplier);

    public void InitializeRuntime(string id, string itemName, Sprite itemIcon, float toolBreakSpeedMultiplier, bool canUseAttackAnimation)
    {
        InitializeRuntime(id, itemName, itemIcon, ItemCategory.Tool);
        breakSpeedMultiplier = Mathf.Max(1f, toolBreakSpeedMultiplier);
        supportsAttackAnimation = canUseAttackAnimation;
    }
}
