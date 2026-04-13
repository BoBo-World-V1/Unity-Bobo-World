using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class BlockDropSpawner : MonoBehaviour
{
    private const float SpawnOffsetMinX = -0.2f;
    private const float SpawnOffsetMaxX = 0.2f;
    private const float SpawnOffsetMinY = 0.1f;
    private const float SpawnOffsetMaxY = 0.3f;

    public static BlockDropSpawner Instance { get; private set; }

    [Header("References")]
    public GameObject droppedBlockPrefab;
    public Transform playerTransform;
    public Inventory inventory;

    private void Awake()
    {
        if (Instance != null && Instance != this){
            Debug.LogWarning("Multiple BlockDropSpawner instances found. Latest instance will be used.");
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this){
            Instance = null;
        }
    }

    public void SpawnDrop(string blockName, TileBase blockTile, Sprite blockSprite, int amount, Vector3 worldPosition)
    {
        if (droppedBlockPrefab == null){
            Debug.LogWarning("DroppedBlock prefab not assigned.");
            return;
        }

        Vector3 spawnPosition = worldPosition + new Vector3(
            Random.Range(SpawnOffsetMinX, SpawnOffsetMaxX),
            Random.Range(SpawnOffsetMinY, SpawnOffsetMaxY),
            0f);

        GameObject drop = Instantiate(droppedBlockPrefab, spawnPosition, Quaternion.identity);
        if (!drop.TryGetComponent(out DroppedBlock droppedBlock)){
            Debug.LogWarning("DroppedBlock prefab is missing the DroppedBlock component.");
            Destroy(drop);
            return;
        }

        droppedBlock.Initialize(blockName, blockTile, blockSprite, amount, playerTransform, inventory);
    }
}
