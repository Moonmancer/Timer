namespace TimerManager;

internal static class SoundGenerator
{
  private const int SampleRate = 44100;
  private const int BitsPerSample = 16;
  private const int Channels = 1;

  /// <summary>
  /// Generates a "timer done" alert: three short beeps at 880 Hz.
  /// Returns a valid PCM WAV byte array ready for SoundPlayer.
  /// </summary>
  public static byte[] CreateTimerBeep()
  {
    // 3 beeps à 160 ms with 110 ms silence between them
    const double beepHz = 520.0;
    const int beepMs = 160;
    const int silenceMs = 110;
    const int beepCount = 3;
    const double amplitude = 28000; // 0..32767

    int beepSamples = SampleRate * beepMs / 1000;
    int silenceSamples = SampleRate * silenceMs / 1000;
    int totalSamples = beepCount * beepSamples + (beepCount - 1) * silenceSamples;

    var samples = new short[totalSamples];
    int pos = 0;

    for (int b = 0; b < beepCount; b++)
    {
      // Beep
      for (int i = 0; i < beepSamples; i++, pos++)
      {
        // Fade-in/out over 10 ms to avoid clicks
        double env = 1.0;
        int fadeSamples = SampleRate * 10 / 1000;
        if (i < fadeSamples) env = (double)i / fadeSamples;
        else if (i > beepSamples - fadeSamples) env = (double)(beepSamples - i) / fadeSamples;

        double t = (double)i / SampleRate;
        samples[pos] = (short)(amplitude * env * Math.Sin(2 * Math.PI * beepHz * t));
      }

      // Silence between beeps
      if (b < beepCount - 1)
      {
        for (int i = 0; i < silenceSamples; i++, pos++)
          samples[pos] = 0;
      }
    }

    return BuildWav(samples);
  }

  private static byte[] BuildWav(short[] samples)
  {
    int dataBytes = samples.Length * (BitsPerSample / 8);
    int byteRate = SampleRate * Channels * (BitsPerSample / 8);
    int blockAlign = Channels * (BitsPerSample / 8);

    using var ms = new System.IO.MemoryStream();
    using var bw = new System.IO.BinaryWriter(ms);

    // RIFF header
    bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
    bw.Write(36 + dataBytes);          // chunk size
    bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

    // fmt chunk
    bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
    bw.Write(16);                      // sub-chunk size
    bw.Write((short)1);               // PCM
    bw.Write((short)Channels);
    bw.Write(SampleRate);
    bw.Write(byteRate);
    bw.Write((short)blockAlign);
    bw.Write((short)BitsPerSample);

    // data chunk
    bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
    bw.Write(dataBytes);
    foreach (var s in samples)
      bw.Write(s);

    return ms.ToArray();
  }
}
