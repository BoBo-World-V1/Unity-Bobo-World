using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Attach to a manager GameObject in the scene.
/// Handles spawning dropped block GameObjects into the world.
/// </summary>
public class BlockDropSpawner : MonoBehaviour
{
    public static BlockDropSpawner Instance { get; private set; }

    [Header("References")]
    public GameObject droppedBlockPrefab;   // Drag DroppedBlock prefab here
    public Transform playerTransform;        // Drag Player here
    public Inventory inventory;              // Drag Player Inventory here

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Spawns a dropped block at the given world position.
    /// </summary>
    public void SpawnDrop(string blockName, TileBase blockTile, Sprite blockSprite, int amount, Vector3 worldPosition)
    {
        if (droppedBlockPrefab == null)
        {
            Debug.LogWarning("DroppedBlock prefab not assigned!");
            return;
        }

        // Slight random offset so drops don't stack perfectly
        Vector3 spawnPos = worldPosition + new Vector3(
            Random.Range(-0.2f, 0.2f),
            Random.Range(0.1f, 0.3f),
            0f
        );

        GameObject drop = Instantiate(droppedBlockPrefab, spawnPos, Quaternion.identity);
        DroppedBlock droppedBlock = drop.GetComponent<DroppedBlock>();

        if (droppedBlock != null)
            droppedBlock.Initialize(blockName, blockTile, blockSprite, amount, playerTransform, inventory);
    }
}