using System;
using System.Linq;
using KeySonic.Core.Keyboard;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeySonic.Core.Audio;

public sealed class AudioEngine : IDisposable
{
    private const int MaxConcurrentVoices = 32;

    private readonly MixingSampleProvider _mixer;
    private readonly WaveOutEvent _output;
    private float _masterVolume = 0.7f;
    private bool _enabled = true;

    public AudioEngine()
    {
        _mixer = new MixingSampleProvider(SoundBank.EngineFormat) { ReadFully = true };

        _output = new WaveOutEvent
        {
            DesiredLatency = 50,
            NumberOfBuffers = 3
        };

        _output.PlaybackStopped += (_, args) =>
        {
            Console.WriteLine($"[AudioEngine] PLAYBACK STOPPED at {DateTime.UtcNow:HH:mm:ss.fff}. Exception: {args.Exception}");
        };

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

    public void Play(CachedSound sound)
    {
        if (!_enabled) return;

        if (_mixer.MixerInputs.Count() >= MaxConcurrentVoices)
        {
            return;
        }

        var voice = new CachedSoundSampleProvider(sound, _masterVolume);
        _mixer.AddMixerInput(voice);
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}