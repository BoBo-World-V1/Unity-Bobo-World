using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Attach to a manager GameObject in the scene.
/// Handles spawning dropped block GameObjects into the world.
/// </summary>
public class BlockDropSpawner : MonoBehaviour
{
    private const float SpawnOffsetMinX = -0.2f;
    private const float SpawnOffsetMaxX = 0.2f;
    private const float SpawnOffsetMinY = 0.1f;
    private const float SpawnOffsetMaxY = 0.3f;

    public static BlockDropSpawner Instance { get; private set; }

    [Header("References")]
    public GameObject droppedBlockPrefab;   // Drag DroppedBlock prefab here
    public Transform playerTransform;        // Drag Player here
    public Inventory inventory;              // Drag Player Inventory here

    private void Awake(){
        if (Instance != null && Instance != this)Debug.LogWarning("Multiple BlockDropSpawner instances found. Latest instance will be used.");

        Instance = this;
    }

    // Spawns a dropped block at the given world position.
    public void SpawnDrop(string blockName, TileBase blockTile, Sprite blockSprite, int amount, Vector3 worldPosition){
        if (droppedBlockPrefab == null){
            Debug.LogWarning("DroppedBlock prefab not assigned!");
            return;
        }

        // Slight random offset so drops don't stack perfectly
        Vector3 spawnPos = worldPosition + new Vector3(
            Random.Range(SpawnOffsetMinX, SpawnOffsetMaxX),
            Random.Range(SpawnOffsetMinY, SpawnOffsetMaxY),
            0f
        );

        GameObject drop = Instantiate(droppedBlockPrefab, spawnPos, Quaternion.identity);
        bool hasDroppedBlock = drop.TryGetComponent(out DroppedBlock droppedBlock);

        if (hasDroppedBlock) droppedBlock.Initialize(blockName, blockTile, blockSprite, amount, playerTransform, inventory);
        else Debug.LogWarning("DroppedBlock prefab is missing DroppedBlock component.");
    }
}