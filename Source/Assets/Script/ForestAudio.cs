using UnityEngine;

public sealed class ForestAudio : MonoBehaviour
{
    private AudioSource music;
    private float lastBurnAt = float.NegativeInfinity;
    private AudioSource effects;
    private AudioSource playerVoice;
    private AudioSource healingVoice;
    private AudioSource knockoutVoice;
    private AudioClip fire;
    private AudioClip[] hunterDeathCries;
    private AudioClip burningCloth;
    private AudioClip groundImpact;

    private void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.clip = Resources.Load<AudioClip>("Firelight/Legionnaire");
        music.loop = true;
        music.volume = 0.5f;
        music.spatialBlend = 0f;
        effects = gameObject.AddComponent<AudioSource>();
        effects.spatialBlend = 0f;
        fire = Resources.Load<AudioClip>("Firelight/Fire");
        hunterDeathCries = Resources.LoadAll<AudioClip>("Firelight/HunterDeath");
        burningCloth = Resources.Load<AudioClip>("Firelight/BurningCloth");
        playerVoice = gameObject.AddComponent<AudioSource>();
        playerVoice.playOnAwake = false;
        playerVoice.spatialBlend = 0f;
        playerVoice.clip = Resources.Load<AudioClip>("Firelight/HunterCry");
        healingVoice = gameObject.AddComponent<AudioSource>();
        healingVoice.clip = Resources.Load<AudioClip>("Firelight/Ruby");
        healingVoice.playOnAwake = false;
        healingVoice.spatialBlend = 0f;
        healingVoice.priority = 24;
        groundImpact = Resources.Load<AudioClip>("Firelight/GroundImpact");
        knockoutVoice = gameObject.AddComponent<AudioSource>();
        knockoutVoice.playOnAwake = false;
        knockoutVoice.spatialBlend = 0f;
        knockoutVoice.priority = 0;
        knockoutVoice.clip = Resources.Load<AudioClip>("Firelight/ArcadeKnockout");
        if (knockoutVoice.clip == null)
            knockoutVoice.clip = Resources.Load<AudioClip>("Firelight/Knockout");
    }

    public void StartMusic()
    {
        if (!music.isPlaying && music.clip != null)
            music.Play();
    }

    public void PlayFire() => Play(fire, 0.68f);
    public void StopMusic() => music.Stop();

    public void PlayHunterBurn()
    {
        bool recent = Time.unscaledTime - lastBurnAt < .27f;
        PlayHunterCry(recent ? .38f : .80f);
        if (!recent)
            Play(burningCloth, .60f);
        lastBurnAt = Time.unscaledTime;
    }

    public void PlayHunterCry(float volume = .85f)
    {
        if (hunterDeathCries.Length > 0)
            Play(hunterDeathCries[Random.Range(0, hunterDeathCries.Length)], volume);
    }
    public void PlayPlayerHurt()
    {
        if (playerVoice.clip == null || playerVoice.isPlaying)
            return;
        playerVoice.volume = .67f;
        playerVoice.Play();
    }
    public void PlayRuby()
    {
        if (healingVoice.clip != null)
            healingVoice.PlayOneShot(healingVoice.clip, .95f);
    }
    public void PlayGroundImpact() => Play(groundImpact, 0.9f);
    public void PlayKnockout()
    {
        if (knockoutVoice.clip != null)
            knockoutVoice.Play();
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null)
            effects.PlayOneShot(clip, volume);
    }
}
