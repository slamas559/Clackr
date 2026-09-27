using NAudio.Wave;

namespace KeySonic.Core.Audio;

public sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _cachedSound;
    private readonly float _volume;
    private long _position;

    public WaveFormat WaveFormat => _cachedSound.WaveFormat;

    public CachedSoundSampleProvider(CachedSound cachedSound, float volume)
    {
        _cachedSound = cachedSound;
        _volume = volume;
        _position = 0;
    }

    private bool _loggedFinish;
    public int Read(float[] buffer, int offset, int count)
    {
        var source = _cachedSound.AudioData;
        long remaining = source.Length - _position;
        int samplesToCopy = (int)System.Math.Min(remaining, count);

        for (int i = 0; i < samplesToCopy; i++)
        {
            buffer[offset + i] = source[_position + i] * _volume;
        }
        _position += samplesToCopy;

        if (samplesToCopy == 0 && !_loggedFinish)
        {
            _loggedFinish = true;
            Console.WriteLine($"[Voice] finished at {DateTime.UtcNow:HH:mm:ss.fff}, total samples played={_position}");
        }

        return samplesToCopy;
    }
}