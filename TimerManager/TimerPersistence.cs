using System.Text.Json;

namespace TimerManager;

internal record TimerData(string Name, long CountdownDurationTicks, string? SoundPath);
internal record WindowSettings(int Width, int Height, int X, int Y, bool Maximized);
internal record AppSettings(List<TimerData> Timers, WindowSettings? Window, int Volume = 100);

internal static class TimerPersistence
{
  private static readonly string FilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "TimerManager", "settings.json");

  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  private static AppSettings LoadRaw()
  {
    try
    {
      if (!File.Exists(FilePath)) return new([], null);
      return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new([], null);
    }
    catch { return new([], null); }
  }

  private static void SaveRaw(AppSettings settings)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
      File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
    catch { }
  }

  public static List<TimerEntry> Load() =>
      LoadRaw().Timers.ConvertAll(d =>
          new TimerEntry(d.Name, TimeSpan.FromTicks(d.CountdownDurationTicks)) { SoundPath = d.SoundPath });

  public static void Save(IEnumerable<TimerEntry> entries)
  {
    var data = entries.Select(e => new TimerData(e.Name, e.CountdownDuration.Ticks, e.SoundPath)).ToList();
    SaveRaw(new(data, LoadRaw().Window));
  }

  public static WindowSettings? LoadWindowSettings() => LoadRaw().Window;

  public static void SaveWindowSettings(WindowSettings win)
  {
    var raw = LoadRaw();
    SaveRaw(new(raw.Timers, win, raw.Volume));
  }

  public static int LoadVolume() => LoadRaw().Volume;

  public static void SaveVolume(int volume)
  {
    var raw = LoadRaw();
    SaveRaw(new(raw.Timers, raw.Window, volume));
  }
}
