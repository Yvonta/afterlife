using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class WavUtility
{
    private const int HEADER_SIZE = 44;

    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[WavUtility] Cannot convert null AudioClip to WAV.");
            return null;
        }

        int totalSamples = clip.samples * clip.channels;
        float[] sampleBuffer = new float[totalSamples];
        clip.GetData(sampleBuffer, 0);

        int pcmByteCount = totalSamples * 2; // 16-bit = 2 bytes per sample

        using (MemoryStream stream = new MemoryStream(HEADER_SIZE + pcmByteCount))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // --- RIFF Header ---
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmByteCount);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // --- Format Chunk ("fmt ") ---
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Sub-chunk 1 size (16 for PCM)
            writer.Write((short)1); // Audio format (1 = PCM uncompressed)
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2); // Byte rate: SampleRate * Channels * (BitsPerSample / 8)
            writer.Write((short)(clip.channels * 2)); // Block align: Channels * (BitsPerSample / 8)
            writer.Write((short)16); // Bits per sample

            // --- Data Chunk ("data") ---
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmByteCount);

            // --- Convert Float Audio Data (-1.0 to 1.0) to 16-bit Signed PCM ---
            for (int i = 0; i < totalSamples; i++)
            {
                // Clamp float sample to [-1.0, 1.0] range before scaling to short
                float clampedSample = Mathf.Clamp(sampleBuffer[i], -1.0f, 1.0f);
                short pcmSample = (short)(clampedSample * 32767f);
                writer.Write(pcmSample);
            }

            return stream.ToArray();
        }
    }
}