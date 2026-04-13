using UnityEngine;

[DisallowMultipleComponent]
public class GameAudio : MonoBehaviour
{
    private const string ResourcesPath = "Audio/SFX";
    private const int SampleRate = 44100;

    private static GameAudio instance;

    [Header("Mixer")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Range(0f, 0.2f)] public float pitchVariance = 0.03f;

    private AudioSource sfxSource;
    private AudioClip blockBreakClip;
    private AudioClip blockPlaceClip;
    private AudioClip pickupClip;
    private AudioClip jumpClip;
    private AudioClip landClip;
    private AudioClip craftClip;
    private AudioClip swingClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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
        ConfigureAudioSource();
        LoadClips();
    }

    public static void PlayBlockBreak()
    {
        EnsureInstance().Play(instance.blockBreakClip, 0.95f, 0.96f);
    }

    public static void PlayBlockPlace()
    {
        EnsureInstance().Play(instance.blockPlaceClip, 0.85f, 1.04f);
    }

    public static void PlayPickup()
    {
        EnsureInstance().Play(instance.pickupClip, 0.9f, 1.08f);
    }

    public static void PlayJump()
    {
        EnsureInstance().Play(instance.jumpClip, 0.9f, 1.02f);
    }

    public static void PlayLand()
    {
        EnsureInstance().Play(instance.landClip, 0.9f, 0.94f);
    }

    public static void PlayCraft()
    {
        EnsureInstance().Play(instance.craftClip, 0.95f, 1f);
    }

    public static void PlaySwing()
    {
        EnsureInstance().Play(instance.swingClip, 0.7f, 1.1f);
    }

    private static GameAudio EnsureInstance()
    {
        if (instance != null){
            return instance;
        }

        instance = FindAnyObjectByType<GameAudio>();
        if (instance != null){
            return instance;
        }

        GameObject audioObject = new("GameAudio");
        instance = audioObject.AddComponent<GameAudio>();
        return instance;
    }

    private void ConfigureAudioSource()
    {
        if (!TryGetComponent(out sfxSource)){
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
    }

    private void LoadClips()
    {
        blockBreakClip = LoadClip("BlockBreak") ?? CreateToneClip("Generated_BlockBreak", 175f, 0.09f, 0.20f, ToneShape.Noise);
        blockPlaceClip = LoadClip("BlockPlace") ?? CreateToneClip("Generated_BlockPlace", 240f, 0.06f, 0.16f, ToneShape.Square);
        pickupClip = LoadClip("Pickup") ?? CreateToneClip("Generated_Pickup", 720f, 0.08f, 0.18f, ToneShape.Sine);
        jumpClip = LoadClip("Jump") ?? CreateToneClip("Generated_Jump", 460f, 0.12f, 0.18f, ToneShape.Sine);
        landClip = LoadClip("Land") ?? CreateToneClip("Generated_Land", 140f, 0.09f, 0.22f, ToneShape.Square);
        craftClip = LoadClip("Craft") ?? CreateChimeClip("Generated_Craft", 520f, 780f, 0.22f, 0.16f);
        swingClip = LoadClip("Swing") ?? CreateToneClip("Generated_Swing", 310f, 0.07f, 0.12f, ToneShape.Saw);
    }

    private AudioClip LoadClip(string clipName)
    {
        return Resources.Load<AudioClip>($"{ResourcesPath}/{clipName}");
    }

    private void Play(AudioClip clip, float volumeScale, float basePitch)
    {
        if (clip == null || sfxSource == null){
            return;
        }

        sfxSource.pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * volumeScale));
    }

    private enum ToneShape
    {
        Sine,
        Square,
        Saw,
        Noise,
    }

    private AudioClip CreateToneClip(string clipName, float frequency, float duration, float amplitude, ToneShape shape)
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++){
            float t = i / (float)SampleRate;
            float envelope = Mathf.Clamp01(1f - (i / (float)sampleCount));
            float wave = GetWaveSample(shape, frequency, t, i);
            samples[i] = wave * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateChimeClip(string clipName, float firstFrequency, float secondFrequency, float duration, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++){
            float t = i / (float)SampleRate;
            float normalized = i / (float)sampleCount;
            float envelope = Mathf.Pow(1f - normalized, 1.6f);
            float first = Mathf.Sin(2f * Mathf.PI * firstFrequency * t);
            float second = Mathf.Sin(2f * Mathf.PI * secondFrequency * Mathf.Max(0f, t - 0.03f));
            samples[i] = (first * 0.65f + second * 0.35f) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private float GetWaveSample(ToneShape shape, float frequency, float time, int index)
    {
        float phase = 2f * Mathf.PI * frequency * time;
        return shape switch
        {
            ToneShape.Sine => Mathf.Sin(phase),
            ToneShape.Square => Mathf.Sign(Mathf.Sin(phase)),
            ToneShape.Saw => 2f * (frequency * time - Mathf.Floor(0.5f + frequency * time)),
            ToneShape.Noise => Mathf.Sin(phase * 0.6f) * 0.35f + (Mathf.PerlinNoise(index * 0.16f, 0.5f) * 2f - 1f) * 0.65f,
            _ => Mathf.Sin(phase),
        };
    }
}
