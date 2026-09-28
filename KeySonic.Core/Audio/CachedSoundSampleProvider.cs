using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeySonic.Core.Audio;

/// <summary>
/// One in-flight playback instance of a CachedSound. Creating one of these does not
/// touch disk or allocate any large buffer - it just holds a position cursor into
/// the already-decoded float array. This is what lets dozens of key sounds overlap:
/// each key press gets its own tiny instance of this class added to the mixer.
///
/// When the sound finishes, it removes itself from the mixer on its own, so callers
/// never need to track or clean up finished voices.
/// </summary>
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

        // Returning fewer samples than requested (including 0 once fully exhausted) tells
        // the owning MixingSampleProvider this voice is done; it prunes finished sources
        // internally, safely, from its own Read() - we must NOT call RemoveMixerInput
        // ourselves from here, since that would mutate the mixer's source list while the
        // mixer is mid-iteration over that same list, which throws and silently kills the
        // whole WASAPI playback thread (this was the "only plays once" bug).
        return samplesToCopy;
    }
}
