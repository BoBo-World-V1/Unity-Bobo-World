using UnityEngine;
using UnityEngine.Tilemaps;

public class DroppedBlock : MonoBehaviour
{
    [Header("Settings")]
    public float pickupRadius = 0.8f;       // How close player needs to be
    public float bobSpeed = 2f;             // Hover bob speed
    public float bobAmplitude = 0.1f;       // Hover bob height
    public float attractSpeed = 5f;         // Speed block flies toward player
    public float attractRadius = 1.5f;      // Distance at which block starts flying to player

    // ── block data ─────────────────────────────────────────────────────────────
    private string blockName;
    private TileBase blockTile;
    private Sprite blockSprite;
    private int amount = 1;

    // ── internals ──────────────────────────────────────────────────────────────
    private Transform player;
    private Inventory inventory;
    private Vector3 startPosition;
    private bool isAttracting;
    private SpriteRenderer sr;

    public void Initialize(string name, TileBase tile, Sprite sprite, int count, Transform playerTransform, Inventory inv)
    {
        blockName = name;
        blockTile = tile;
        blockSprite = sprite;
        amount = count;
        player = playerTransform;
        inventory = inv;

        // Set sprite to block sprite
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && blockSprite != null)
            sr.sprite = blockSprite;

        // Set drop size here
        transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        startPosition = transform.position;
    }

    private void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        // Start attracting when player is close enough
        if (dist < attractRadius)
            isAttracting = true;

        if (isAttracting)
        {
            // Fly toward player
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                attractSpeed * Time.deltaTime
            );

            // Pickup when close enough
            if (dist < pickupRadius)
                Pickup();
        }
        else
        {
            // Idle hover bob
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    private void Pickup()
    {
        if (inventory.AddBlock(blockName, blockTile, blockSprite, amount))
        {
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