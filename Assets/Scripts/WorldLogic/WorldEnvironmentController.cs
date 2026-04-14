using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class WorldEnvironmentController : MonoBehaviour
{
    private enum WeatherType
    {
        Clear,
        Rain,
        Storm,
        Snow,
    }

    private const int FlashSortingOrder = 32000;
    private const int WeatherSortingOrder = 31998;
    private const float FlashOverlayZ = 5f;
    private const float WeatherLocalZ = 4f;
    private const float WeatherSpawnPadding = 1.5f;
    private const string BackgroundTilemapName = "Background";

    private static WorldEnvironmentController instance;
    private static Sprite whiteSprite;
    private static Texture2D rainTexture;
    private static Texture2D snowTexture;

    [Header("Day / Night")]
    [Range(30f, 600f)] public float fullDayDurationSeconds = 180f;
    [Range(0f, 1f)] public float startTimeOfDay = 0.25f;
    public Color dawnSkyColor = new(0.97f, 0.69f, 0.51f, 1f);
    public Color daySkyColor = new(0.47f, 0.77f, 0.98f, 1f);
    public Color duskSkyColor = new(0.95f, 0.48f, 0.31f, 1f);
    public Color nightSkyColor = new(0.05f, 0.10f, 0.20f, 1f);

    [Header("Background Tint")]
    public Color dawnBackgroundTint = new(1f, 0.90f, 0.82f, 1f);
    public Color dayBackgroundTint = Color.white;
    public Color duskBackgroundTint = new(0.90f, 0.76f, 0.70f, 1f);
    public Color nightBackgroundTint = new(0.38f, 0.45f, 0.62f, 1f);

    [Header("Weather")]
    [Range(10f, 180f)] public float weatherDurationMin = 35f;
    [Range(10f, 180f)] public float weatherDurationMax = 80f;
    [Range(0f, 1f)] public float rainChance = 0.35f;
    [Range(0f, 1f)] public float stormChance = 0.15f;
    [Range(0f, 1f)] public float snowChance = 0.15f;
    public Color rainSkyTint = new(0.73f, 0.80f, 0.88f, 1f);
    public Color stormSkyTint = new(0.33f, 0.41f, 0.54f, 1f);
    public Color snowSkyTint = new(0.90f, 0.94f, 0.99f, 1f);
    public Color rainBackgroundTint = new(0.86f, 0.91f, 0.97f, 1f);
    public Color stormBackgroundTint = new(0.56f, 0.64f, 0.78f, 1f);
    public Color snowBackgroundTint = new(0.97f, 0.98f, 1f, 1f);

    private Camera targetCamera;
    private Transform cameraTransform;
    private Tilemap backgroundTilemap;
    private SpriteRenderer flashOverlayRenderer;
    private ParticleSystem weatherParticles;
    private ParticleSystemRenderer weatherParticleRenderer;
    private Material weatherMaterial;

    private float timeOfDay;
    private float nextWeatherChangeTime;
    private float lightningTimer;
    private float lightningFlash;
    private WeatherType currentWeather;
    private WeatherType appliedWeather = (WeatherType)(-1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (instance != null && instance != this){
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        timeOfDay = Mathf.Repeat(startTimeOfDay, 1f);
        EnsureSceneReferences();
        EnsureFlashOverlay();
        EnsureWeatherParticles();
        PickNextWeather(true);
        ApplyEnvironment(true);
    }

    private void Update()
    {
        if (!EnsureSceneReferences()){
            return;
        }

        UpdateTimeOfDay();
        UpdateWeatherTimers();
        ApplyEnvironment(false);
    }

    private static WorldEnvironmentController EnsureInstance()
    {
        if (instance != null){
            return instance;
        }

        instance = FindAnyObjectByType<WorldEnvironmentController>();
        if (instance != null){
            return instance;
        }

        GameObject controllerObject = new("WorldEnvironmentController");
        instance = controllerObject.AddComponent<WorldEnvironmentController>();
        return instance;
    }

    private bool EnsureSceneReferences()
    {
        Camera main = Camera.main;
        if (main == null){
            return false;
        }

        bool cameraChanged = main != targetCamera || cameraTransform == null;
        targetCamera = main;
        cameraTransform = targetCamera.transform;

        if (backgroundTilemap == null){
            backgroundTilemap = FindBackgroundTilemap();
        }

        if (cameraChanged){
            EnsureFlashOverlay();
            EnsureWeatherParticles();
        }

        return true;
    }

    private Tilemap FindBackgroundTilemap()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        foreach (Tilemap tilemap in tilemaps){
            if (tilemap != null && tilemap.name == BackgroundTilemapName){
                return tilemap;
            }
        }

        return null;
    }

    private void UpdateTimeOfDay()
    {
        float duration = Mathf.Max(1f, fullDayDurationSeconds);
        timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / duration, 1f);
    }

    private void UpdateWeatherTimers()
    {
        if (Time.time >= nextWeatherChangeTime){
            PickNextWeather(false);
        }

        if (currentWeather == WeatherType.Storm){
            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0f){
                lightningFlash = 1f;
                lightningTimer = Random.Range(3.5f, 8f);
            }
        }

        lightningFlash = Mathf.MoveTowards(lightningFlash, 0f, Time.deltaTime * 4.5f);
    }

    private void PickNextWeather(bool immediate)
    {
        float roll = Random.value;
        if (roll < stormChance){
            currentWeather = WeatherType.Storm;
        }
        else if (roll < stormChance + rainChance){
            currentWeather = WeatherType.Rain;
        }
        else if (roll < stormChance + rainChance + snowChance){
            currentWeather = WeatherType.Snow;
        }
        else{
            currentWeather = WeatherType.Clear;
        }

        float duration = immediate ? Random.Range(weatherDurationMin, Mathf.Max(weatherDurationMin, weatherDurationMax)) : Random.Range(weatherDurationMin, Mathf.Max(weatherDurationMin, weatherDurationMax));
        nextWeatherChangeTime = Time.time + duration;
        lightningTimer = Random.Range(3f, 7f);
        ConfigureWeatherIfNeeded();
    }

    private void ApplyEnvironment(bool force)
    {
        if (targetCamera == null){
            return;
        }

        ConfigureWeatherIfNeeded();

        Color baseSkyColor = EvaluateSkyColor(timeOfDay);
        Color baseBackgroundTint = EvaluateBackgroundTint(timeOfDay);

        Color finalSkyColor = ApplyWeatherToSky(baseSkyColor);
        Color finalBackgroundTint = ApplyWeatherToBackground(baseBackgroundTint);

        if (currentWeather == WeatherType.Storm && lightningFlash > 0f){
            finalSkyColor = Color.Lerp(finalSkyColor, Color.white, lightningFlash * 0.45f);
            finalBackgroundTint = Color.Lerp(finalBackgroundTint, Color.white, lightningFlash * 0.18f);
        }

        targetCamera.backgroundColor = finalSkyColor;

        if (backgroundTilemap != null){
            backgroundTilemap.color = finalBackgroundTint;
        }

        if (flashOverlayRenderer != null){
            flashOverlayRenderer.color = new Color(0.84f, 0.91f, 1f, lightningFlash * 0.12f);
            UpdateFlashOverlayTransform(force);
        }

        if (weatherParticles != null){
            UpdateWeatherTransform(force);
        }
    }

    private Color EvaluateSkyColor(float normalizedTime)
    {
        if (normalizedTime < 0.25f){
            return Color.Lerp(nightSkyColor, dawnSkyColor, normalizedTime / 0.25f);
        }

        if (normalizedTime < 0.5f){
            return Color.Lerp(dawnSkyColor, daySkyColor, (normalizedTime - 0.25f) / 0.25f);
        }

        if (normalizedTime < 0.75f){
            return Color.Lerp(daySkyColor, duskSkyColor, (normalizedTime - 0.5f) / 0.25f);
        }

        return Color.Lerp(duskSkyColor, nightSkyColor, (normalizedTime - 0.75f) / 0.25f);
    }

    private Color EvaluateBackgroundTint(float normalizedTime)
    {
        if (normalizedTime < 0.25f){
            return Color.Lerp(nightBackgroundTint, dawnBackgroundTint, normalizedTime / 0.25f);
        }

        if (normalizedTime < 0.5f){
            return Color.Lerp(dawnBackgroundTint, dayBackgroundTint, (normalizedTime - 0.25f) / 0.25f);
        }

        if (normalizedTime < 0.75f){
            return Color.Lerp(dayBackgroundTint, duskBackgroundTint, (normalizedTime - 0.5f) / 0.25f);
        }

        return Color.Lerp(duskBackgroundTint, nightBackgroundTint, (normalizedTime - 0.75f) / 0.25f);
    }

    private Color ApplyWeatherToSky(Color baseSkyColor)
    {
        return currentWeather switch
        {
            WeatherType.Rain => Color.Lerp(baseSkyColor, rainSkyTint, 0.35f),
            WeatherType.Storm => Color.Lerp(baseSkyColor, stormSkyTint, 0.62f),
            WeatherType.Snow => Color.Lerp(baseSkyColor, snowSkyTint, 0.24f),
            _ => baseSkyColor,
        };
    }

    private Color ApplyWeatherToBackground(Color baseBackgroundTint)
    {
        return currentWeather switch
        {
            WeatherType.Rain => Color.Lerp(baseBackgroundTint, rainBackgroundTint, 0.20f),
            WeatherType.Storm => Color.Lerp(baseBackgroundTint, stormBackgroundTint, 0.38f),
            WeatherType.Snow => Color.Lerp(baseBackgroundTint, snowBackgroundTint, 0.22f),
            _ => baseBackgroundTint,
        };
    }

    private void EnsureFlashOverlay()
    {
        if (cameraTransform == null){
            return;
        }

        if (flashOverlayRenderer == null){
            GameObject overlayObject = new("FlashOverlay");
            overlayObject.transform.SetParent(cameraTransform, false);
            flashOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
            flashOverlayRenderer.sprite = GetWhiteSprite();
            flashOverlayRenderer.maskInteraction = SpriteMaskInteraction.None;
            flashOverlayRenderer.sortingOrder = FlashSortingOrder;
        }

        flashOverlayRenderer.transform.localPosition = new Vector3(0f, 0f, FlashOverlayZ);
        UpdateFlashOverlayTransform(true);
    }

    private void UpdateFlashOverlayTransform(bool force)
    {
        if (flashOverlayRenderer == null || targetCamera == null){
            return;
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        Vector3 desiredScale = new(width, height, 1f);
        if (force || flashOverlayRenderer.transform.localScale != desiredScale){
            flashOverlayRenderer.transform.localScale = desiredScale;
        }
    }

    private void EnsureWeatherParticles()
    {
        if (cameraTransform == null){
            return;
        }

        if (weatherParticles == null){
            GameObject weatherObject = new("WeatherParticles");
            weatherObject.transform.SetParent(cameraTransform, false);
            weatherParticles = weatherObject.AddComponent<ParticleSystem>();
            weatherParticleRenderer = weatherObject.GetComponent<ParticleSystemRenderer>();
            weatherMaterial = new Material(Shader.Find("Sprites/Default"));
            weatherParticleRenderer.material = weatherMaterial;
            weatherParticleRenderer.sortingOrder = WeatherSortingOrder;
            ConfigureParticleDefaults();
        }

        weatherParticles.transform.localPosition = new Vector3(0f, 0f, WeatherLocalZ);
        UpdateWeatherTransform(true);
        ConfigureWeatherIfNeeded();
    }

    private void ConfigureParticleDefaults()
    {
        var main = weatherParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 700;
        main.startRotation = 0f;
        main.stopAction = ParticleSystemStopAction.None;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = weatherParticles.emission;
        emission.enabled = true;

        var shape = weatherParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        var velocityOverLifetime = weatherParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;

        var colorOverLifetime = weatherParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var noise = weatherParticles.noise;
        noise.enabled = false;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.separateAxes = true;
    }

    private void UpdateWeatherTransform(bool force)
    {
        if (weatherParticles == null || targetCamera == null){
            return;
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;

        var shape = weatherParticles.shape;
        shape.scale = new Vector3(width + WeatherSpawnPadding, height + WeatherSpawnPadding, 1f);

        Vector3 localPosition = new(0f, height * 0.55f, WeatherLocalZ);
        if (force || weatherParticles.transform.localPosition != localPosition){
            weatherParticles.transform.localPosition = localPosition;
        }
    }

    private void ConfigureWeatherIfNeeded()
    {
        if (weatherParticles == null || currentWeather == appliedWeather){
            return;
        }

        appliedWeather = currentWeather;

        var main = weatherParticles.main;
        var emission = weatherParticles.emission;
        var velocity = weatherParticles.velocityOverLifetime;
        var colorOverLifetime = weatherParticles.colorOverLifetime;
        var noise = weatherParticles.noise;

        switch (currentWeather){
            case WeatherType.Rain:
                ApplyParticleTexture(GetRainTexture());
                weatherParticleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                weatherParticleRenderer.lengthScale = 2.2f;
                weatherParticleRenderer.velocityScale = 0.34f;
                main.startLifetime = 1.0f;
                main.startSpeed = 0f;
                main.startSizeX = 0.035f;
                main.startSizeY = 0.42f;
                main.startColor = new Color(0.78f, 0.90f, 1f, 0.78f);
                emission.rateOverTime = 115f;
                velocity.x = 1.1f;
                velocity.y = -15f;
                velocity.z = 0f;
                colorOverLifetime.color = BuildGradient(
                    new Color(0.80f, 0.90f, 1f, 0f),
                    new Color(0.80f, 0.90f, 1f, 0.82f),
                    new Color(0.80f, 0.90f, 1f, 0f));
                noise.enabled = false;
                ResumeWeatherIfNeeded();
                break;

            case WeatherType.Storm:
                ApplyParticleTexture(GetRainTexture());
                weatherParticleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                weatherParticleRenderer.lengthScale = 2.7f;
                weatherParticleRenderer.velocityScale = 0.42f;
                main.startLifetime = 0.85f;
                main.startSpeed = 0f;
                main.startSizeX = 0.04f;
                main.startSizeY = 0.56f;
                main.startColor = new Color(0.82f, 0.92f, 1f, 0.88f);
                emission.rateOverTime = 165f;
                velocity.x = 2.2f;
                velocity.y = -18f;
                velocity.z = 0f;
                colorOverLifetime.color = BuildGradient(
                    new Color(0.84f, 0.93f, 1f, 0f),
                    new Color(0.84f, 0.93f, 1f, 0.92f),
                    new Color(0.84f, 0.93f, 1f, 0f));
                noise.enabled = false;
                ResumeWeatherIfNeeded();
                break;

            case WeatherType.Snow:
                ApplyParticleTexture(GetSnowTexture());
                weatherParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                weatherParticleRenderer.lengthScale = 1f;
                weatherParticleRenderer.velocityScale = 0f;
                main.startLifetime = 4.6f;
                main.startSpeed = 0f;
                main.startSizeX = 0.12f;
                main.startSizeY = 0.12f;
                main.startColor = new Color(1f, 1f, 1f, 0.94f);
                emission.rateOverTime = 34f;
                velocity.x = 0.25f;
                velocity.y = -2.25f;
                velocity.z = 0f;
                colorOverLifetime.color = BuildGradient(
                    new Color(1f, 1f, 1f, 0f),
                    new Color(1f, 1f, 1f, 0.94f),
                    new Color(1f, 1f, 1f, 0f));
                noise.enabled = true;
                noise.strengthX = 0.65f;
                noise.strengthY = 0.12f;
                noise.frequency = 0.13f;
                noise.scrollSpeed = 0.16f;
                ResumeWeatherIfNeeded();
                break;

            default:
                emission.rateOverTime = 0f;
                noise.enabled = false;
                break;
        }
    }

    private void ResumeWeatherIfNeeded()
    {
        if (!weatherParticles.isPlaying){
            weatherParticles.Play();
        }
    }

    private void ApplyParticleTexture(Texture2D texture)
    {
        if (weatherMaterial != null && texture != null){
            weatherMaterial.mainTexture = texture;
        }
    }

    private ParticleSystem.MinMaxGradient BuildGradient(Color start, Color middle, Color end)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.2f),
                new GradientColorKey(end, 1f),
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(middle.a, 0.18f),
                new GradientAlphaKey(end.a, 1f),
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null){
            return whiteSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0u, SpriteMeshType.FullRect);
        return whiteSprite;
    }

    private static Texture2D GetRainTexture()
    {
        if (rainTexture != null){
            return rainTexture;
        }

        rainTexture = new Texture2D(8, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        for (int y = 0; y < rainTexture.height; y++){
            float v = y / (float)(rainTexture.height - 1);
            float alpha = Mathf.Sin(v * Mathf.PI) * 0.95f;

            for (int x = 0; x < rainTexture.width; x++){
                float u = x / (float)(rainTexture.width - 1);
                float centerFalloff = 1f - Mathf.Abs(u - 0.5f) * 2f;
                rainTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * Mathf.Clamp01(centerFalloff)));
            }
        }

        rainTexture.Apply();
        return rainTexture;
    }

    private static Texture2D GetSnowTexture()
    {
        if (snowTexture != null){
            return snowTexture;
        }

        snowTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Vector2 center = new(11.5f, 11.5f);
        float radius = 10.5f;

        for (int y = 0; y < snowTexture.height; y++){
            for (int x = 0; x < snowTexture.width; x++){
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float radial = Mathf.Clamp01(1f - distance / radius);
                float alpha = radial * radial;
                snowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        snowTexture.Apply();
        return snowTexture;
    }
}
