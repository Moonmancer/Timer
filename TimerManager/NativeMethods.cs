namespace TimerManager;

using System.Runtime.InteropServices;
using System.Text;

internal static class NativeMethods
{
  /// <summary>
  /// Sets the volume for the current application's waveOut audio (Windows Volume Mixer).
  /// </summary>
  [DllImport("winmm.dll")]
  internal static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

  /// <summary>
  /// MCI command interface – plays any media Windows supports (WAV, MP3, …).
  /// </summary>
  [DllImport("winmm.dll", CharSet = CharSet.Auto)]
  internal static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);

  /// <summary>
  /// Plays a file via MCI on a dedicated STA thread (required by winmm).
  /// Returns true on success, false if MCI failed to open the file.
  /// </summary>
  internal static bool MciPlay(string filePath)
  {
    bool success = false;
    var t = new System.Threading.Thread(() =>
    {
      string alias = "snd_" + Guid.NewGuid().ToString("N")[..8];
      string safePath = filePath.Replace("\"", "\\\"");
      if (mciSendString($"open \"{safePath}\" alias {alias}", null, 0, IntPtr.Zero) != 0)
        return;
      success = true;
      mciSendString($"play {alias} wait", null, 0, IntPtr.Zero);
      mciSendString($"close {alias}", null, 0, IntPtr.Zero);
    });
    t.SetApartmentState(System.Threading.ApartmentState.STA);
    t.IsBackground = true;
    t.Start();
    t.Join();
    return success;
  }
}
