using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeySonic.Core.Audio;

/// <summary>
/// A sound file, fully decoded into memory exactly once at load time and converted
/// to the engine's common format (44.1kHz, stereo, 32-bit float). Playing it never
/// touches disk again - PlaySound just hands out a cheap read cursor over this buffer.
/// </summary>
public sealed class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }
    public string SourcePath { get; }

    public CachedSound(string audioFilePath, WaveFormat targetFormat)
    {
        SourcePath = audioFilePath;
        WaveFormat = targetFormat;

        using var reader = new AudioFileReader(audioFilePath);
        ISampleProvider sampleProvider = reader;

        if (reader.WaveFormat.Channels == 1 && targetFormat.Channels == 2)
        {
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
        }
        else if (reader.WaveFormat.Channels == 2 && targetFormat.Channels == 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }

        if (reader.WaveFormat.SampleRate != targetFormat.SampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetFormat.SampleRate);
        }

        var wholeFile = new List<float>((int)(reader.Length / 2));
        var readBuffer = new float[targetFormat.SampleRate * targetFormat.Channels];
        int samplesRead;
        while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            wholeFile.AddRange(new System.ArraySegment<float>(readBuffer, 0, samplesRead));
        }

        AudioData = wholeFile.ToArray();
    }
}
