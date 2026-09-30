using System;
using System.Linq;
using KeySonic.Core.Keyboard;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeySonic.Core.Audio;

/// <summary>
/// Owns exactly one WASAPI output stream for the lifetime of the application.
/// The stream is never opened or closed per key press - that is the #1 way people
/// accidentally add latency to this kind of app. Instead, each key press just adds
/// a lightweight sample provider into an always-running mix.
///
/// Voice cap: to protect against unbounded growth during extremely fast/sustained
/// typing (or a stuck key), concurrent voices are capped. Once at the cap, new key
/// presses are simply not given a new voice until one finishes - this is a deliberate,
/// simple choice for Phase 1 rather than building a full priority/stealing scheme.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int MaxConcurrentVoices = 32;

    private readonly MixingSampleProvider _mixer;
    private readonly WasapiOut _output;
    private float _masterVolume = 0.7f;
    private bool _enabled = true;

    public AudioEngine()
    {
        _mixer = new MixingSampleProvider(SoundBank.EngineFormat) { ReadFully = true };

        // Shared mode + event sync with a short latency hint keeps us off the default
        // ~200ms WASAPI buffer some drivers pick, without needing exclusive mode.
        _output = new WasapiOut(AudioClientShareMode.Shared, useEventSync: true, latency: 20);
        _output.Init(_mixer);
        _output.Play();
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Triggers immediate playback of an already-decoded sound. Safe to call rapidly
    /// and concurrently (e.g. from the keyboard hook thread) - this only does a
    /// dictionary-free, allocation-light add into the mixer's input list.
    /// </summary>
    public void Play(CachedSound sound)
    {
        if (!_enabled) return;

        PlayCore(sound, _masterVolume);
    }

    public void PlayPreview(CachedSound sound)
    {
        PlayCore(sound, _masterVolume);
    }

    public void PlayPreview(CachedSound sound, float volume)
    {
        PlayCore(sound, volume);
    }

    public void PlayMouseClick(CachedSound sound, float volume)
    {
        PlayCore(sound, volume);
    }

    private void PlayCore(CachedSound sound, float volume)
    {
        if (_mixer.MixerInputs.Count() >= MaxConcurrentVoices)
        {
            // Deliberately drop this voice rather than let inputs grow unbounded.
            return;
        }

        var voice = new CachedSoundSampleProvider(sound, Math.Clamp(volume, 0f, 1f));
        _mixer.AddMixerInput(voice);
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}
