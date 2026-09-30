using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Vorbis;
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
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");

    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }
    public string SourcePath { get; }

    public CachedSound(string audioFilePath, WaveFormat targetFormat)
    {
        SourcePath = audioFilePath;
        WaveFormat = targetFormat;

        if (Path.GetExtension(audioFilePath).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            if (TryReadExtensiblePcm24Format(audioFilePath, out var sourceFormat))
            {
                using var waveReader = new WaveFileReader(audioFilePath);
                AudioData = ConvertToEngineFormat(
                    new Pcm24BitWaveSampleProvider(waveReader), sourceFormat, targetFormat);
                return;
            }
        }

        if (Path.GetExtension(audioFilePath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            using var vorbisReader = new VorbisWaveReader(audioFilePath);
            AudioData = ConvertToEngineFormat(new WaveToSampleProvider(vorbisReader), vorbisReader.WaveFormat, targetFormat);
            return;
        }

        using var reader = new AudioFileReader(audioFilePath);
        AudioData = ConvertToEngineFormat(reader, reader.WaveFormat, targetFormat);
    }

    private static float[] ConvertToEngineFormat(
        ISampleProvider source,
        WaveFormat sourceFormat,
        WaveFormat targetFormat)
    {
        ISampleProvider sampleProvider = source;
        if (sourceFormat.Channels == 1 && targetFormat.Channels == 2)
        {
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
        }
        else if (sourceFormat.Channels == 2 && targetFormat.Channels == 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }

        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetFormat.SampleRate);
        }

        var wholeFile = new List<float>();
        var readBuffer = new float[targetFormat.SampleRate * targetFormat.Channels];
        int samplesRead;
        while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            wholeFile.AddRange(new System.ArraySegment<float>(readBuffer, 0, samplesRead));
        }

        return wholeFile.ToArray();
    }

    private static bool TryReadExtensiblePcm24Format(string path, out WaveFormat sourceFormat)
    {
        sourceFormat = default!;
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF") return false;
        reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE") return false;

        while (stream.Position + 8 <= stream.Length)
        {
            string chunkId = new(reader.ReadChars(4));
            uint chunkSize = reader.ReadUInt32();
            long chunkEnd = stream.Position + chunkSize;
            if (chunkEnd > stream.Length) return false;

            if (chunkId == "fmt ")
            {
                if (chunkSize < 40) return false;

                ushort formatTag = reader.ReadUInt16();
                ushort channels = reader.ReadUInt16();
                int sampleRate = checked((int)reader.ReadUInt32());
                reader.ReadUInt32();
                reader.ReadUInt16();
                ushort bitsPerSample = reader.ReadUInt16();
                ushort extraSize = reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt32();
                Guid subFormat = new(reader.ReadBytes(16));

                if (formatTag != 0xFFFE || extraSize < 22 || bitsPerSample != 24 ||
                    subFormat != PcmSubFormat || channels == 0 || sampleRate <= 0)
                {
                    return false;
                }

                sourceFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
                return true;
            }

            stream.Position = chunkEnd + (chunkSize & 1);
        }

        return false;
    }

    private sealed class Pcm24BitWaveSampleProvider : ISampleProvider
    {
        private readonly WaveFileReader _reader;

        public WaveFormat WaveFormat { get; }

        public Pcm24BitWaveSampleProvider(WaveFileReader reader)
        {
            _reader = reader;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int requestedBytes = count * 3;
            var bytes = new byte[requestedBytes];
            int bytesRead = _reader.Read(bytes, 0, requestedBytes);
            int samplesRead = bytesRead / 3;

            for (int sampleIndex = 0; sampleIndex < samplesRead; sampleIndex++)
            {
                int byteIndex = sampleIndex * 3;
                int sample = bytes[byteIndex] | (bytes[byteIndex + 1] << 8) | (bytes[byteIndex + 2] << 16);
                if ((sample & 0x00800000) != 0)
                {
                    sample |= unchecked((int)0xFF000000);
                }

                buffer[offset + sampleIndex] = sample / 8388608f;
            }

            return samplesRead;
        }
    }
}
