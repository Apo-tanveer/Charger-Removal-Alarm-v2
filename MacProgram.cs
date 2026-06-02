using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;

namespace ChargerRemovalAlarm
{
    // ── Entry point ───────────────────────────────────────────────────────────
    class Program
    {
        [STAThread]
        static void Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToTrace();
    }

    // ── App ───────────────────────────────────────────────────────────────────
    public class App : Application
    {
        public override void Initialize() { }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }

    // ── Power status (macOS via pmset) ────────────────────────────────────────
    static class PowerHelper
    {
        public static (bool pluggedIn, int percent) GetStatus()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("pmset", "-g batt")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow  = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                string output  = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                bool plugged = output.Contains("AC Power") ||
                               output.Contains("'AC attached; not charging'") ||
                               output.Contains("charged");

                int percent = 100;
                var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
                if (match.Success) percent = int.Parse(match.Groups[1].Value);

                return (plugged, percent);
            }
            catch { return (true, 100); }
        }
    }

    // ── Alarm audio (macOS via afplay + generated wav temp file) ─────────────
    static class AlarmAudio
    {
        private const int SR = 44100;
        private static string _tmpFile;

        public static string GetWavPath()
        {
            if (_tmpFile != null && System.IO.File.Exists(_tmpFile)) return _tmpFile;

            int n = (int)(2.5 * SR); var pcm = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                double t  = (double)i / SR;
                double sw = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 2 * t);
                double fr = 880 + sw * 880;
                double v  = 0.6 * Math.Sin(2 * Math.PI * fr * t)
                          + 0.3 * Math.Sin(2 * Math.PI * fr * 2 * t)
                          + 0.1 * Math.Sin(2 * Math.PI * fr * 3 * t);
                v *= 0.7 + 0.3 * Math.Sin(2 * Math.PI * 8 * t);
                short s16 = (short)Math.Max(-32767, Math.Min(32767, v * 32767));
                pcm[i*2] = (byte)(s16 & 0xFF); pcm[i*2+1] = (byte)((s16 >> 8) & 0xFF);
            }
            int dl = pcm.Length; var wav = new byte[44 + dl];
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, wav, 0, 4);
            BitConverter.GetBytes(36+dl).CopyTo(wav,4);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "), 0, wav, 8, 8);
            BitConverter.GetBytes(16).CopyTo(wav,16); BitConverter.GetBytes((short)1).CopyTo(wav,20);
            BitConverter.GetBytes((short)1).CopyTo(wav,22); BitConverter.GetBytes(SR).CopyTo(wav,24);
            BitConverter.GetBytes(SR*2).CopyTo(wav,28); BitConverter.GetBytes((short)2).CopyTo(wav,32);
            BitConverter.GetBytes((short)16).CopyTo(wav,34);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("data"), 0, wav, 36, 4);
            BitConverter.GetBytes(dl).CopyTo(wav,40); Buffer.BlockCopy(pcm,0,wav,44,dl);

            _tmpFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "charger_alarm.wav");
            System.IO.File.WriteAllBytes(_tmpFile, wav);
            return _tmpFile;
        }
    }

    // ── Main Window ───────────────────────────────────────────────────────────
    public class MainWindow : Window
    {
        // Colors
        private static readonly IBrush Purple      = new SolidColorBrush(Color.FromRgb(99, 91, 219));
        private static readonly IBrush PurpleLight = new SolidColorBrush(Color.FromRgb(120, 112, 235));
        private static readonly IBrush BgPage      = new SolidColorBrush(Color.FromRgb(245, 245, 252));
        private static readonly IBrush IconCircleB = new SolidColorBrush(Color.FromRgb(230, 228, 248));
        private static readonly IBrush TextPurple  = new SolidColorBrush(Color.FromRgb(85, 75, 210));
        private static readonly IBrush TextGray    = new SolidColorBrush(Color.FromRgb(140, 138, 160));
        private static readonly IBrush BarBgB      = new SolidColorBrush(Color.FromRgb(220, 218, 245));
        private static readonly IBrush GreenOk     = new SolidColorBrush(Color.FromRgb(60, 190, 100));
        private static readonly IBrush RedAlert    = new SolidColorBrush(Color.FromRgb(235, 70, 70));
        private static readonly IBrush OrangeWarn  = new SolidColorBrush(Color.FromRgb(235, 140, 40));
        private static readonly IBrush White       = Brushes.White;

        // Controls
        private Border       _header;
        private TextBlock    _iconText;
        private Ellipse      _iconCircle;
        private TextBlock    _statusLabel;
        private TextBlock    _batteryLabel;
        private Border       _barFg;
        private Grid         _barContainer;
        private Button       _silenceBtn;
        private Button       _toggleBtn;
        private TextBlock    _monitorDot;
        private TextBlock    _monitorLabel;

        // State
        private bool _monitoring  = true;
        private bool _alarmActive = false;
        private bool _silenced    = false;
        private bool _lastPlugged = true;
        private bool _flashState  = false;
        private DispatcherTimer _pollTimer;
        private DispatcherTimer _flashTimer;
        private CancellationTokenSource _alarmCts;

        public MainWindow()
        {
            Title   = "Charger Removal Alarm";
            Width   = 420;
            Height  = 500;
            CanResize = false;
            Background = BgPage;

            BuildUI();

            var (plugged, pct) = PowerHelper.GetStatus();
            _lastPlugged = plugged;
            UpdateDisplay(plugged, pct);

            _pollTimer  = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick  += OnPollTick;
            _pollTimer.Start();

            _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _flashTimer.Tick += OnFlashTick;
        }

        private void BuildUI()
        {
            // ── Header ───────────────────────────────────────────────────────
            _header = new Border
            {
                Background = Purple,
                Height     = 78,
                Child      = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⚡  CHARGER REMOVAL ALARM",
                            FontSize   = 16,
                            FontWeight = FontWeight.Bold,
                            Foreground = White,
                            TextAlignment = Avalonia.Media.TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Alerts when charger is disconnected",
                            FontSize   = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 255)),
                            TextAlignment = Avalonia.Media.TextAlignment.Center
                        }
                    }
                }
            };

            // ── Icon circle ───────────────────────────────────────────────────
            _iconCircle = new Ellipse
            {
                Width = 100, Height = 100,
                Fill  = IconCircleB
            };
            _iconText = new TextBlock
            {
                Text      = "⚡",
                FontSize  = 38,
                Foreground = Purple,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            var iconGrid = new Grid { Width = 100, Height = 100, Margin = new Thickness(0, 24, 0, 0) };
            iconGrid.Children.Add(_iconCircle);
            iconGrid.Children.Add(_iconText);

            // ── Labels ────────────────────────────────────────────────────────
            _statusLabel = new TextBlock
            {
                Text       = "CHARGER CONNECTED",
                FontSize   = 16,
                FontWeight = FontWeight.Bold,
                Foreground = TextPurple,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };
            _batteryLabel = new TextBlock
            {
                Text       = "Battery: 100%",
                FontSize   = 11,
                Foreground = TextGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            // ── Battery bar ───────────────────────────────────────────────────
            _barFg = new Border
            {
                Background          = Purple,
                CornerRadius        = new CornerRadius(4),
                Height              = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width               = 356
            };
            _barContainer = new Grid { Margin = new Thickness(32, 12, 32, 0) };
            var barBgBorder = new Border
            {
                Background   = BarBgB,
                CornerRadius = new CornerRadius(4),
                Height       = 8
            };
            _barContainer.Children.Add(barBgBorder);
            _barContainer.Children.Add(_barFg);

            // ── Silence button ────────────────────────────────────────────────
            _silenceBtn = MakeButton("🔔  SILENCE ALARM", Purple, White, true);
            _silenceBtn.IsEnabled = false;
            _silenceBtn.Click    += (s, e) => SilenceAlarm();

            // ── Toggle button ─────────────────────────────────────────────────
            var softPurple = new SolidColorBrush(Color.FromRgb(232, 230, 248));
            var darkPurpleTxt = new SolidColorBrush(Color.FromRgb(80, 78, 110));
            _toggleBtn = MakeButton("⏸  PAUSE MONITORING", softPurple, darkPurpleTxt, false);
            _toggleBtn.Click += (s, e) => ToggleMonitoring();

            // ── Monitor dot ───────────────────────────────────────────────────
            _monitorDot = new TextBlock
            {
                Text = "●", FontSize = 10,
                Foreground = GreenOk,
                VerticalAlignment = VerticalAlignment.Center
            };
            _monitorLabel = new TextBlock
            {
                Text = "Monitoring active", FontSize = 10,
                Foreground = TextGray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            var dotRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Children = { _monitorDot, _monitorLabel }
            };

            // ── Layout ────────────────────────────────────────────────────────
            var body = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    iconGrid, _statusLabel, _batteryLabel, _barContainer,
                    new Border { Height = 10 },
                    _silenceBtn, _toggleBtn, dotRow
                }
            };

            Content = new StackPanel
            {
                Children = { _header, body }
            };
        }

        private Button MakeButton(string text, IBrush bg, IBrush fg, bool bold)
        {
            var btn = new Button
            {
                Content             = text,
                Background          = bg,
                Foreground          = fg,
                FontSize            = bold ? 14 : 12,
                FontWeight          = bold ? FontWeight.Bold : FontWeight.Normal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Height              = bold ? 52 : 46,
                CornerRadius        = new CornerRadius(14),
                Margin              = new Thickness(32, 6, 32, 0)
            };
            return btn;
        }

        private void UpdateDisplay(bool plugged, int percent)
        {
            double barWidth = Math.Max(0, 356 * percent / 100.0);
            _barFg.Width       = barWidth;
            _batteryLabel.Text = $"Battery: {percent}%";

            if (plugged)
            {
                _iconText.Text      = "⚡";
                _iconText.Foreground = Purple;
                _iconCircle.Fill    = IconCircleB;
                _statusLabel.Text   = "CHARGER CONNECTED";
                _statusLabel.Foreground = TextPurple;
                _barFg.Background   = Purple;
            }
            else
            {
                var c = _silenced ? OrangeWarn : RedAlert;
                _iconText.Text      = "🔋";
                _iconText.Foreground = c;
                _iconCircle.Fill    = new SolidColorBrush(Color.FromRgb(248, 225, 225));
                _statusLabel.Text   = _silenced ? "UNPLUGGED (SILENCED)" : "CHARGER DISCONNECTED";
                _statusLabel.Foreground = c;
                _barFg.Background   = c;
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (!_monitoring) return;
            var (plugged, pct) = PowerHelper.GetStatus();
            UpdateDisplay(plugged, pct);
            if (_lastPlugged && !plugged && !_alarmActive) StartAlarm();
            if (!_lastPlugged && plugged) { StopAlarm(); _silenced = false; }
            _lastPlugged = plugged;
        }

        private void StartAlarm()
        {
            _alarmActive = true; _silenced = false;
            _silenceBtn.IsEnabled  = true;
            _silenceBtn.Background = RedAlert;
            _flashTimer.Start();
            _alarmCts = new CancellationTokenSource();
            var tok = _alarmCts.Token;
            Task.Run(() => {
                string wav = AlarmAudio.GetWavPath();
                while (!tok.IsCancellationRequested)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("afplay", wav)
                        { UseShellExecute = false, CreateNoWindow = true };
                    using var p = System.Diagnostics.Process.Start(psi);
                    // Wait until done or cancelled
                    while (!p.HasExited && !tok.IsCancellationRequested)
                        Thread.Sleep(100);
                    if (tok.IsCancellationRequested) { try { p.Kill(); } catch { } }
                }
            }, tok);
        }

        private void StopAlarm()
        {
            if (!_alarmActive) return;
            _alarmCts?.Cancel(); _alarmActive = false;
            _silenceBtn.IsEnabled  = false;
            _silenceBtn.Background = Purple;
            _flashTimer.Stop();
            _header.Background = Purple;
        }

        private void SilenceAlarm()
        {
            _silenced = true;
            StopAlarm();
            var (plugged, pct) = PowerHelper.GetStatus();
            UpdateDisplay(plugged, pct);
        }

        private void OnFlashTick(object sender, EventArgs e)
        {
            _flashState = !_flashState;
            _header.Background = _flashState
                ? new SolidColorBrush(Color.FromRgb(220, 50, 50))
                : new SolidColorBrush(Color.FromRgb(180, 30, 30));
        }

        private void ToggleMonitoring()
        {
            _monitoring = !_monitoring;
            if (_monitoring)
            {
                _toggleBtn.Content     = "⏸  PAUSE MONITORING";
                _monitorDot.Foreground = GreenOk;
                _monitorLabel.Text     = "Monitoring active";
            }
            else
            {
                StopAlarm();
                _toggleBtn.Content     = "▶  RESUME MONITORING";
                _monitorDot.Foreground = TextGray;
                _monitorLabel.Text     = "Monitoring paused";
            }
        }
    }
}
