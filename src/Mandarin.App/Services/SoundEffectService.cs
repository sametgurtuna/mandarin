using System;
using System.IO;
using System.Media;

namespace Mandarin.App.Services;

/// <summary>
/// Provides subtle, low-latency audio feedback when navigating radial menu petals
/// or completing conversions, matching Tangerine's interactive sound design.
/// </summary>
public static class SoundEffectService
{
    private static SoundPlayer? _tickPlayer;
    private static SoundPlayer? _successPlayer;
    private static readonly object _lock = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                var tickWav = CreateTickWav();
                _tickPlayer = new SoundPlayer(new MemoryStream(tickWav));
                _tickPlayer.Load();

                var successWav = CreateSuccessWav();
                _successPlayer = new SoundPlayer(new MemoryStream(successWav));
                _successPlayer.Load();

                _initialized = true;
            }
            catch
            {
                // Audio failure shouldn't crash the app
            }
        }
    }

    public static void PlayTick()
    {
        try
        {
            if (!_initialized) Initialize();
            _tickPlayer?.Play();
        }
        catch
        {
        }
    }

    public static void PlaySuccess()
    {
        try
        {
            if (!_initialized) Initialize();
            _successPlayer?.Play();
        }
        catch
        {
        }
    }

    private static byte[] CreateTickWav()
    {
        // 44.1 kHz, 16-bit mono subtle click (~18ms)
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.018);
        var pcmData = new short[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double decay = Math.Exp(-i / (sampleRate * 0.0035));
            double freq = 1800 - (i * 0.4);
            double sample = Math.Sin(2 * Math.PI * freq * t) * decay * 0.35;
            pcmData[i] = (short)(sample * short.MaxValue);
        }

        return WrapPcmInWav(pcmData, sampleRate, 1);
    }

    private static byte[] CreateSuccessWav()
    {
        // 44.1 kHz, 16-bit mono pleasant soft chime (~120ms)
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.12);
        var pcmData = new short[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double decay = Math.Exp(-i / (sampleRate * 0.03));
            double sample = (Math.Sin(2 * Math.PI * 880 * t) * 0.25 + Math.Sin(2 * Math.PI * 1320 * t) * 0.15) * decay;
            pcmData[i] = (short)(sample * short.MaxValue);
        }

        return WrapPcmInWav(pcmData, sampleRate, 1);
    }

    private static byte[] WrapPcmInWav(short[] pcmData, int sampleRate, short channels)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int subchunk2Size = pcmData.Length * channels * 2;
        int chunkSize = 36 + subchunk2Size;

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // Subchunk1Size
        bw.Write((short)1); // PCM
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * 2); // ByteRate
        bw.Write((short)(channels * 2)); // BlockAlign
        bw.Write((short)16); // BitsPerSample
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(subchunk2Size);

        for (int i = 0; i < pcmData.Length; i++)
        {
            bw.Write(pcmData[i]);
        }

        return ms.ToArray();
    }
}
