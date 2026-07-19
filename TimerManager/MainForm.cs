using System.Diagnostics;

namespace TimerManager;

public class MainForm : Form
{
    private readonly Panel _timerListPanel;
    private readonly Button _btnAdd;
    private readonly System.Windows.Forms.Timer _globalTick;
    private readonly List<TimerControl> _timerControls = [];
    private readonly Label _lblEmpty;
    private readonly Panel _updateBanner;
    private readonly Label _lblUpdate;
    private bool _closeConfirmed;

    // Drag-to-reorder state
    private TimerControl? _dragCtrl;
    private Point _dragAnchor;
    private bool _dragging;

    public MainForm()
    {
        Text = "Timer Manager";
        Size = new Size(480, 600);
        MinimumSize = new Size(200, 150);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.White;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(36, 36, 36),
            Padding = new Padding(10, 10, 10, 0)
        };

        var _btnPin = new Button
        {
            Text = "",
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe MDL2 Assets", 11),
            Height = 30,
            Width = 36,
            Location = new Point(10, 10),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            UseCompatibleTextRendering = false
        };
        _btnPin.Click += (_, _) =>
        {
            TopMost = !TopMost;
            _btnPin.BackColor = TopMost ? Color.FromArgb(0, 100, 180) : Color.FromArgb(60, 60, 60);
        };

        _btnAdd = new Button
        {
            Text = "",
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe MDL2 Assets", 11),
            Height = 30,
            Width = 36,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            UseCompatibleTextRendering = false
        };
        _btnAdd.Click += (_, _) => AddTimer();

        // ── Volume control ────────────────────────────────
        var _lblVolume = new Label
        {
            Text = "100%",
            ForeColor = Color.FromArgb(180, 180, 195),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Width = 40,
            Height = 30,
            Location = new Point(54, 10),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var _trackVolume = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 100,
            TickStyle = TickStyle.None,
            Height = 30,
            Location = new Point(90, 10),
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(36, 36, 36)
        };

        void ApplyVolume(int pct)
        {
            uint v = (uint)(pct * 0xFFFF / 100);
            uint packed = v | (v << 16);
            NativeMethods.waveOutSetVolume(IntPtr.Zero, packed);
        }

        // Gespeicherte Lautstärke laden
        int savedVolume = TimerPersistence.LoadVolume();
        _trackVolume.Value = savedVolume;
        _lblVolume.Text = $"{savedVolume}%";
        ApplyVolume(savedVolume);

        _trackVolume.ValueChanged += (_, _) =>
        {
            _lblVolume.Text = $"{_trackVolume.Value}%";
            ApplyVolume(_trackVolume.Value);
            TimerPersistence.SaveVolume(_trackVolume.Value);
        };

        toolbar.Controls.AddRange([_btnPin, _lblVolume, _trackVolume, _btnAdd]);
        toolbar.Resize += (_, _) =>
        {
            _btnAdd.Location = new Point(toolbar.Width - _btnAdd.Width - 10, 10);
            _trackVolume.Width = toolbar.Width - _btnAdd.Width - _trackVolume.Left - 20;
        };

        // Scrollable timer list
        _timerListPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(10, 10, 10, 0)
        };

        _lblEmpty = new Label
        {
            Text = "Noch keine Timer.\nKlicke auf \"+ Timer hinzufügen\".",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 11),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _timerListPanel.Controls.Add(_lblEmpty);

        // Update-Banner (unten, anfangs versteckt)
        _updateBanner = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Color.FromArgb(0, 100, 180),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _lblUpdate = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        _updateBanner.Controls.Add(_lblUpdate);

        Controls.Add(_timerListPanel);
        Controls.Add(_updateBanner);
        Controls.Add(toolbar);

        // Global tick – aktualisiert alle Timer gleichzeitig
        _globalTick = new System.Windows.Forms.Timer { Interval = 250 };
        _globalTick.Tick += (_, _) => RefreshAll();
        _globalTick.Start();

        // Gespeicherte Timer wiederherstellen (umgekehrt laden, da BringToFront die Reihenfolge invertiert)
        foreach (var entry in TimerPersistence.Load().AsEnumerable().Reverse())
            AddTimerControl(entry);

        // Fenstereinstellungen wiederherstellen
        var win = TimerPersistence.LoadWindowSettings();
        if (win is not null)
        {
            Size = new Size(win.Width, win.Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(win.X, win.Y)))
                Location = new Point(win.X, win.Y);
            if (win.Maximized)
                WindowState = FormWindowState.Maximized;
            if (win.TopMost)
            {
                TopMost = true;
                _btnPin.BackColor = Color.FromArgb(0, 100, 180);
            }
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        // Beim Start im Hintergrund auf neue Version prüfen
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        int current = UpdateChecker.CurrentVersion;
        if (current <= 0) return;  // lokaler Dev-Build ohne Versionsstempel

        var info = await UpdateChecker.CheckAsync();
        if (info is null || info.Version <= current || IsDisposed) return;

        BeginInvoke(() =>
        {
            _lblUpdate.Text = $"⬇  Update verfügbar (v{info.Version}) – klicken zum Herunterladen";
            _updateBanner.Visible = true;

            void Open(object? _, EventArgs __)
            {
                try { Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true }); }
                catch { /* Browser konnte nicht geöffnet werden */ }
            }
            _updateBanner.Click += Open;
            _lblUpdate.Click += Open;
        });
    }

    private void AddTimer()
    {
        using var dlg = new AddTimerDialog { TopMost = TopMost };
        if (dlg.ShowDialog(this) is not DialogResult.OK) return;
        AddTimerControl(new TimerEntry(dlg.TimerName, dlg.CountdownDuration)
        {
            SoundPath = dlg.SoundPath,
            AccentColorArgb = dlg.AccentColorArgb
        });
        Save();
    }

    private void AddTimerControl(TimerEntry entry)
    {
        var ctrl = new TimerControl(entry);
        ctrl.RemoveRequested += (s, _) => RemoveTimer((TimerControl)s!);
        ctrl.EditRequested += (s, _) => EditTimer((TimerControl)s!);
        ctrl.StateChanged += (_, _) => Save();
        ctrl.Finished += (_, _) => BringToForeground();
        ctrl.RegisterDragHandlers(TimerDrag_MouseDown, TimerDrag_MouseMove, TimerDrag_MouseUp);

        _timerControls.Insert(0, ctrl);
        _timerListPanel.Controls.Add(ctrl);
        ctrl.BringToFront();
        _lblEmpty.Visible = false;
    }

    // ── Drag-to-reorder ───────────────────────────────────────────────

    private TimerControl? GetTimerControl(object? sender) =>
        sender as TimerControl ?? (sender as Control)?.Parent as TimerControl;

    private void TimerDrag_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragCtrl = GetTimerControl(sender);
        if (_dragCtrl == null) return;
        var screenPt = ((Control)sender!).PointToScreen(e.Location);
        _dragAnchor = _dragCtrl.PointToClient(screenPt);
        _dragging = false;
    }

    private void TimerDrag_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragCtrl == null || e.Button != MouseButtons.Left) return;

        var screenPt = ((Control)sender!).PointToScreen(e.Location);

        if (!_dragging)
        {
            var anchorScreen = _dragCtrl.PointToScreen(_dragAnchor);
            if (Math.Abs(screenPt.Y - anchorScreen.Y) < 6) return;
            _dragging = true;
            _dragCtrl.IsDragging = true;
            _dragCtrl.Cursor = Cursors.SizeNS;
            _dragCtrl.Invalidate();
        }

        // Mouse Y in panel's scrollable coordinate space
        var panelPt = _timerListPanel.PointToClient(screenPt);
        int scrolledY = panelPt.Y - _timerListPanel.AutoScrollPosition.Y;

        // Sort all OTHER timer controls by their current Y position
        var others = _timerListPanel.Controls
            .OfType<TimerControl>()
            .Where(c => c != _dragCtrl)
            .OrderBy(c => c.Top)
            .ToList();

        int targetIndex = others.Count; // default: after all others
        for (int i = 0; i < others.Count; i++)
        {
            if (scrolledY < others[i].Top + others[i].Height / 2)
            {
                targetIndex = i;
                break;
            }
        }

        _timerListPanel.Controls.SetChildIndex(_dragCtrl, others.Count - targetIndex);
    }

    private void TimerDrag_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_dragCtrl == null) return;

        _dragCtrl.IsDragging = false;
        _dragCtrl.Cursor = Cursors.Default;
        _dragCtrl.Invalidate();

        if (_dragging)
        {
            // Sync _timerControls list to match the new visual (z-order) sequence
            _timerControls.Clear();
            _timerControls.AddRange(_timerListPanel.Controls.OfType<TimerControl>());
            Save();
        }

        _dragCtrl = null;
        _dragging = false;
    }

    private void EditTimer(TimerControl ctrl)
    {
        using var dlg = new AddTimerDialog(ctrl.Entry) { TopMost = TopMost };
        if (dlg.ShowDialog(this) is not DialogResult.OK) return;
        ctrl.Entry.Name = dlg.TimerName;
        ctrl.Entry.CountdownDuration = dlg.CountdownDuration;
        ctrl.Entry.SoundPath = dlg.SoundPath;
        ctrl.Entry.AccentColorArgb = dlg.AccentColorArgb;
        ctrl.Entry.Reset();
        ctrl.RefreshAfterEdit();
        Save();
    }

    private void RemoveTimer(TimerControl ctrl)
    {
        _timerControls.Remove(ctrl);
        _timerListPanel.Controls.Remove(ctrl);
        ctrl.Dispose();
        _lblEmpty.Visible = _timerControls.Count == 0;
        Save();
    }

    private void Save() => TimerPersistence.Save(_timerControls.Select(c => c.Entry));

    /// <summary>Holt das Fenster in den Vordergrund (z. B. wenn ein Timer abläuft).</summary>
    private void BringToForeground()
    {
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        Show();
        // Kurzes TopMost-Umschalten hebt das Fenster zuverlässig über andere Fenster;
        // ist der Pin aktiv, bleibt TopMost ohnehin erhalten.
        bool wasTopMost = TopMost;
        TopMost = true;
        TopMost = wasTopMost;
        Activate();
        BringToFront();
    }

    private void RefreshAll()
    {
        foreach (var ctrl in _timerControls)
            ctrl.Update();
        UpdateWindowTitle();
    }

    private const string BaseTitle = "Timer Manager";

    private void UpdateWindowTitle()
    {
        // Priorität: abgelaufene Timer, dann laufende mit kürzester Restzeit
        var finished = _timerControls.FirstOrDefault(c => c.Entry.State is TimerState.Finished);
        if (finished is not null)
        {
            Text = $"⏰ Fertig: {finished.Entry.Name} – {BaseTitle}";
            return;
        }

        TimerControl? soonest = null;
        var best = TimeSpan.MaxValue;
        foreach (var c in _timerControls)
        {
            if (c.Entry.State is not TimerState.Running) continue;
            var rem = c.Entry.GetDisplay();
            if (rem < best) { best = rem; soonest = c; }
        }

        Text = soonest is not null
            ? $"⏱ {best:hh\\:mm\\:ss} – {soonest.Entry.Name}"
            : BaseTitle;
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || _closeConfirmed) return;

        // Bei Windows-Herunterfahren / Task-Manager nicht aufhalten
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing) return;

        int current = UpdateChecker.CurrentVersion;
        if (current <= 0) return;  // lokaler Dev-Build ohne Versionsstempel → sofort schließen

        // Schließen kurz aufschieben, um einmalig auf ein Update zu prüfen (max. 3 s)
        e.Cancel = true;

        var checkTask = UpdateChecker.CheckAsync();
        var finished = await Task.WhenAny(checkTask, Task.Delay(3000));
        var info = finished == checkTask ? checkTask.Result : null;

        if (info is not null && info.Version > current)
        {
            var answer = MessageBox.Show(this,
                $"Eine neue Version (v{info.Version}) ist verfügbar.\n\nJetzt die Download-Seite öffnen?",
                "Update verfügbar", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer is DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true }); }
                catch { /* Browser konnte nicht geöffnet werden */ }
            }
        }

        _closeConfirmed = true;
        Close();  // jetzt tatsächlich schließen
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _globalTick.Stop();
        Save();  // aktuellen Laufzeit-Zustand aller Timer sichern
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        TimerPersistence.SaveWindowSettings(new(
            bounds.Width, bounds.Height, bounds.X, bounds.Y,
            WindowState == FormWindowState.Maximized, TopMost));
        base.OnFormClosed(e);
    }
}
