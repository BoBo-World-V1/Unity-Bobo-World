using UnityEngine;

public static class GeneratedItemIcons
{
    private const int IconSize = 16;

    private static Sprite stonePickaxeIcon;

    public static Sprite GetStonePickaxeIcon()
    {
        if (stonePickaxeIcon == null){
            stonePickaxeIcon = CreateStonePickaxeIcon();
        }

        return stonePickaxeIcon;
    }

    private static Sprite CreateStonePickaxeIcon()
    {
        Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color handle = new Color32(115, 79, 44, 255);
        Color handleShadow = new Color32(76, 50, 27, 255);
        Color stone = new Color32(158, 162, 171, 255);
        Color stoneShadow = new Color32(98, 103, 112, 255);
        Color highlight = new Color32(220, 224, 232, 255);

        for (int y = 0; y < IconSize; y++){
            for (int x = 0; x < IconSize; x++){
                texture.SetPixel(x, y, transparent);
            }
        }

        DrawLine(texture, 5, 2, 9, 10, handleShadow, 2);
        DrawLine(texture, 6, 2, 10, 10, handle, 2);
        DrawLine(texture, 5, 9, 2, 13, stoneShadow, 2);
        DrawLine(texture, 6, 10, 3, 14, stone, 2);
        DrawLine(texture, 6, 10, 11, 15, stoneShadow, 2);
        DrawLine(texture, 7, 10, 12, 15, stone, 2);
        DrawLine(texture, 3, 13, 12, 13, stoneShadow, 2);
        DrawLine(texture, 3, 14, 12, 14, stone, 1);
        DrawLine(texture, 4, 14, 11, 14, highlight, 1);

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f), IconSize);
        sprite.name = "Generated_StonePickaxe";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true){
            DrawBrush(texture, x0, y0, color, thickness);

            if (x0 == x1 && y0 == y1){
                break;
            }

            int e2 = err * 2;
            if (e2 > -dy){
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx){
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawBrush(Texture2D texture, int centerX, int centerY, Color color, int size)
    {
        int radius = Mathf.Max(0, size - 1);

        for (int y = -radius; y <= radius; y++){
            for (int x = -radius; x <= radius; x++){
                int pixelX = centerX + x;
                int pixelY = centerY + y;

                if (pixelX < 0 || pixelX >= texture.width || pixelY < 0 || pixelY >= texture.height){
                    continue;
                }

                texture.SetPixel(pixelX, pixelY, color);
            }
        }
    }
}
