using UnityEngine;

[System.Serializable]
public struct CraftingIngredient
{
    public string itemId;
    public int amount;

    public CraftingIngredient(string ingredientItemId, int ingredientAmount)
    {
        itemId = ingredientItemId;
        amount = ingredientAmount;
    }
}

[CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Game/Crafting/Crafting Recipe")]
public class CraftingRecipeDefinition : ScriptableObject
{
    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;
    [SerializeField] private ItemDefinition output;
    [SerializeField] private int outputCount = 1;
    [SerializeField] private CraftingIngredient[] ingredients;

    public string RecipeId => recipeId;
    public string DisplayName => displayName;
    public ItemDefinition Output => output;
    public int OutputCount => outputCount;
    public CraftingIngredient[] Ingredients => ingredients;

    public void InitializeRuntime(string id, string name, ItemDefinition outputItem, int count, params CraftingIngredient[] recipeIngredients)
    {
        recipeId = id;
        displayName = name;
        output = outputItem;
        outputCount = Mathf.Max(1, count);
        ingredients = recipeIngredients;
        hideFlags = HideFlags.HideAndDontSave;
        this.name = $"CraftingRecipe_{id}";
    }
}
