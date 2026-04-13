using UnityEngine;
using UnityEngine.Tilemaps;

public class DroppedBlock : MonoBehaviour
{
    private static readonly Vector3 DropScale = new(0.5f, 0.5f, 1f);

    [Header("Settings")]
    public float pickupRadius = 0.8f;
    public float bobSpeed = 2f;
    public float bobAmplitude = 0.1f;
    public float attractSpeed = 5f;
    public float attractRadius = 1.5f;

    private string blockName;
    private TileBase blockTile;
    private Sprite blockSprite;
    private int amount = 1;
    private Transform player;
    private Inventory inventory;
    private Vector3 startPosition;
    private bool isAttracting;
    private SpriteRenderer sr;

    private void Awake()
    {
        TryGetComponent(out sr);
    }

    public void Initialize(string name, TileBase tile, Sprite sprite, int count, Transform playerTransform, Inventory inv)
    {
        blockName = name;
        blockTile = tile;
        blockSprite = sprite;
        amount = count;
        player = playerTransform;
        inventory = inv;

        if (sr == null){
            TryGetComponent(out sr);
        }

        if (sr != null && blockSprite != null){
            sr.sprite = blockSprite;
        }

        transform.localScale = DropScale;
        startPosition = transform.position;
    }

    private void Update()
    {
        if (player == null){
            ApplyIdleBob();
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        float distanceSqr = toPlayer.sqrMagnitude;

        if (distanceSqr < attractRadius * attractRadius){
            isAttracting = true;
        }

        if (!isAttracting){
            ApplyIdleBob();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, player.position, attractSpeed * Time.deltaTime);

        if (distanceSqr < pickupRadius * pickupRadius){
            Pickup();
        }
    }

    private void ApplyIdleBob()
    {
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = new Vector3(startPosition.x, startPosition.y + bobOffset, startPosition.z);
    }

    private void Pickup()
    {
        if (inventory == null){
            Debug.LogWarning("DroppedBlock has no inventory reference.");
            return;
        }

        if (inventory.AddBlock(blockName, blockTile, blockSprite, amount)){
            Debug.Log($"Picked up {blockName} x{amount}");
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attractRadius);
    }
}
