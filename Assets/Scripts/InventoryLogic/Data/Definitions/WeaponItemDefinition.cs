using UnityEngine;

[CreateAssetMenu(fileName = "WeaponItem", menuName = "Game/Items/Weapon Item")]
public class WeaponItemDefinition : ItemDefinition
{
    [SerializeField] private float damage = 1f;
    [SerializeField] private float range = 3f;
    [SerializeField] private float cooldown = 0.3f;

    public override bool SupportsAttackAnimation => true;
    public float Damage => damage;
    public float Range => range;
    public float Cooldown => cooldown;

    public void InitializeRuntime(string id, string itemName, Sprite itemIcon, float weaponDamage, float weaponRange, float weaponCooldown)
    {
        InitializeRuntime(id, itemName, itemIcon, ItemCategory.Weapon);
        damage = Mathf.Max(0f, weaponDamage);
        range = Mathf.Max(0.1f, weaponRange);
        cooldown = Mathf.Max(0.01f, weaponCooldown);
    }
}
