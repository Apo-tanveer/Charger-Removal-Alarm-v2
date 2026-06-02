using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChargerRemovalAlarm
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ── Power API ─────────────────────────────────────────────────────────────
    static class PowerHelper
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte Reserved1;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        public static (bool pluggedIn, int percent) GetStatus()
        {
            if (!GetSystemPowerStatus(out var s)) return (true, 100);
            return (s.ACLineStatus == 1, s.BatteryLifePercent == 255 ? 100 : (int)s.BatteryLifePercent);
        }
    }

    // ── Audio ─────────────────────────────────────────────────────────────────
    static class AlarmAudio
    {
        private const int SR = 44100;
        public static byte[] GenerateWav(double dur = 2.5)
        {
            int n = (int)(dur * SR); var pcm = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SR;
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
            return wav;
        }
    }

    // ── Circle icon control (fully custom painted — no ghost bleed) ───────────
    public class CircleIconControl : Control
    {
        private string _symbol = "⚡";
        private Color  _symbolColor = Color.FromArgb(99, 91, 219);
        private Color  _circleColor = Color.FromArgb(230, 228, 248);
        private Color  _bgColor     = Color.FromArgb(245, 245, 252);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Symbol      { get => _symbol;      set { _symbol = value;      Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color  SymbolColor { get => _symbolColor; set { _symbolColor = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color  CircleColor { get => _circleColor; set { _circleColor = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color  BgColor     { get => _bgColor;     set { _bgColor = value;     Invalidate(); } }

        public CircleIconControl() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Fill parent background first to avoid ghost
            g.Clear(_bgColor);
            // Draw circle
            using var b = new SolidBrush(_circleColor);
            g.FillEllipse(b, 2, 2, Width - 4, Height - 4);
            // Draw symbol
            using var font = new Font("Segoe UI Emoji", Width * 0.28f, FontStyle.Regular, GraphicsUnit.Point);
            using var sb   = new SolidBrush(_symbolColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_symbol, font, sb, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // ── Rounded button ────────────────────────────────────────────────────────
    public class RoundedButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int   Radius    { get; set; } = 14;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HoverColor { get; set; }

        private bool _hov = false;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hov = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hov = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Fill entire control with parent background first to kill black corners
            Color parentBg = Parent != null ? Parent.BackColor : Color.FromArgb(245, 245, 252);
            e.Graphics.Clear(parentBg);

            Color fill = (_hov && HoverColor != Color.Empty) ? HoverColor : BackColor;
            if (!Enabled) fill = Color.FromArgb(160, fill.R, fill.G, fill.B);

            using var path  = RoundRect(ClientRectangle, Radius);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(160, ForeColor), flags);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath(); int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom-d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    // ── Main Form ─────────────────────────────────────────────────────────────
    public class MainForm : Form
    {
        private static readonly Color Purple       = Color.FromArgb(99,  91, 219);
        private static readonly Color PurpleLight  = Color.FromArgb(120, 112, 235);
        private static readonly Color BgPage       = Color.FromArgb(245, 245, 252);
        private static readonly Color IconCircleC  = Color.FromArgb(230, 228, 248);
        private static readonly Color TextPurple   = Color.FromArgb(85,  75, 210);
        private static readonly Color TextGray     = Color.FromArgb(140, 138, 160);
        private static readonly Color BarBgC       = Color.FromArgb(220, 218, 245);
        private static readonly Color GreenOk      = Color.FromArgb(60,  190, 100);
        private static readonly Color RedAlert     = Color.FromArgb(235, 70,  70);
        private static readonly Color OrangeWarn   = Color.FromArgb(235, 140, 40);

        private Panel            _header;
        private Label            _headerTitle;
        private Label            _headerSub;
        private CircleIconControl _iconCtrl;
        private Label            _statusLabel;
        private Label            _batteryLabel;
        private Panel            _barBg;
        private Panel            _barFg;
        private RoundedButton    _silenceBtn;
        private RoundedButton    _toggleBtn;
        private Label            _monitoringDot;
        private Label            _monitoringLabel;
        private NotifyIcon       _trayIcon;
        private ContextMenuStrip _trayMenu;

        private bool _monitoring  = true;
        private bool _alarmActive = false;
        private bool _silenced    = false;
        private bool _lastPlugged = true;
        private bool _flashState  = false;
        private System.Windows.Forms.Timer _pollTimer;
        private System.Windows.Forms.Timer _flashTimer;
        private CancellationTokenSource    _alarmCts;

        public MainForm()
        {
            BuildUI();
            SetupTray();
            var (plugged, _) = PowerHelper.GetStatus();
            _lastPlugged = plugged;
            _pollTimer  = new System.Windows.Forms.Timer { Interval = 2000 };
            _pollTimer.Tick  += OnPollTick;
            _pollTimer.Start();
            _flashTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _flashTimer.Tick += OnFlashTick;
            UpdateDisplay(plugged, PowerHelper.GetStatus().percent);
        }

        private void BuildUI()
        {
            this.Text            = "Charger Removal Alarm";
            this.Size            = new Size(440, 520);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox     = false;
            this.BackColor       = BgPage;
            this.StartPosition   = FormStartPosition.CenterScreen;

            // Load icon.ico from same folder as exe
            try
            {
                string iconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { /* use default if icon missing */ }

            // ── Header ──────────────────────────────────────────────────────
            _header = new Panel { Bounds = new Rectangle(0, 0, 440, 78), BackColor = Purple };

            _headerTitle = new Label
            {
                Text = "⚡  CHARGER REMOVAL ALARM",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Purple,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 12, 440, 30)
            };
            _headerSub = new Label
            {
                Text = "Alerts when charger is disconnected",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(200, 200, 255), BackColor = Purple,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 44, 440, 22)
            };
            _header.Controls.AddRange(new Control[] { _headerTitle, _headerSub });

            // ── Circle icon (custom painted, no bleed) ───────────────────────
            _iconCtrl = new CircleIconControl
            {
                Symbol      = "⚡",
                SymbolColor = Purple,
                CircleColor = IconCircleC,
                BgColor     = BgPage,
                Size        = new Size(100, 100),
                Location    = new Point(170, 96)
            };

            // ── Status labels ────────────────────────────────────────────────
            _statusLabel = new Label
            {
                Text = "CHARGER CONNECTED",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextPurple, BackColor = BgPage,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 208, 440, 30)
            };
            _batteryLabel = new Label
            {
                Text = "Battery: 100%",
                Font = new Font("Segoe UI", 10),
                ForeColor = TextGray, BackColor = BgPage,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 240, 440, 22)
            };

            // ── Battery bar ───────────────────────────────────────────────────
            _barBg = new Panel { BackColor = BarBgC, Bounds = new Rectangle(32, 274, 376, 8) };
            _barFg = new Panel { BackColor = Purple, Bounds = new Rectangle(0, 0, 376, 8) };
            _barBg.Controls.Add(_barFg);

            // ── Silence button ────────────────────────────────────────────────
            _silenceBtn = new RoundedButton
            {
                Text = "🔔  SILENCE ALARM",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Purple,
                HoverColor = PurpleLight, Radius = 14,
                Bounds = new Rectangle(32, 302, 376, 52),
                Enabled = false, Cursor = Cursors.Hand
            };
            _silenceBtn.Click += (s, e) => SilenceAlarm();

            // ── Toggle button ─────────────────────────────────────────────────
            _toggleBtn = new RoundedButton
            {
                Text = "⏸  PAUSE MONITORING",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(80, 78, 110),
                BackColor = Color.FromArgb(232, 230, 248),
                HoverColor = Color.FromArgb(218, 215, 242),
                Radius = 14,
                Bounds = new Rectangle(32, 366, 376, 48),
                Cursor = Cursors.Hand
            };
            _toggleBtn.Click += (s, e) => ToggleMonitoring();

            // ── Status dot ────────────────────────────────────────────────────
            _monitoringDot = new Label
            {
                Text = "●", Font = new Font("Segoe UI", 9),
                ForeColor = GreenOk, BackColor = BgPage,
                AutoSize = true, Location = new Point(158, 430)
            };
            _monitoringLabel = new Label
            {
                Text = "Monitoring active", Font = new Font("Segoe UI", 9),
                ForeColor = TextGray, BackColor = BgPage,
                AutoSize = true, Location = new Point(176, 430)
            };

            // Add in correct z-order: header first (back), icon on top
            this.Controls.Add(_header);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_batteryLabel);
            this.Controls.Add(_barBg);
            this.Controls.Add(_silenceBtn);
            this.Controls.Add(_toggleBtn);
            this.Controls.Add(_monitoringDot);
            this.Controls.Add(_monitoringLabel);
            this.Controls.Add(_iconCtrl);   // icon last = topmost
        }

        private void SetupTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; });
            _trayMenu.Items.Add("Exit", null, (s, e) => { _trayIcon.Visible = false; Application.Exit(); });

            // Use icon.ico for tray too
            Icon trayIco = SystemIcons.Application;
            try {
                string p = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(p)) trayIco = new Icon(p, 16, 16);
            } catch { }

            _trayIcon = new NotifyIcon { Text = "Charger Removal Alarm", Icon = trayIco, Visible = true, ContextMenuStrip = _trayMenu };
            _trayIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; };
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (!_monitoring) return;
            var (plugged, percent) = PowerHelper.GetStatus();
            UpdateDisplay(plugged, percent);
            if (_lastPlugged && !plugged && !_alarmActive) StartAlarm();
            if (!_lastPlugged && plugged) { StopAlarm(); _silenced = false; }
            _lastPlugged = plugged;
        }

        private void UpdateDisplay(bool plugged, int percent)
        {
            _batteryLabel.Text = $"Battery: {percent}%";
            _barFg.Width = Math.Max(0, (int)(_barBg.Width * percent / 100.0));

            if (plugged)
            {
                _iconCtrl.Symbol      = "⚡";
                _iconCtrl.SymbolColor = Purple;
                _iconCtrl.CircleColor = IconCircleC;
                _statusLabel.Text     = "CHARGER CONNECTED";
                _statusLabel.ForeColor = TextPurple;
                _barFg.BackColor      = Purple;
                _trayIcon.Text        = "Charger Removal Alarm — Plugged in";
            }
            else
            {
                Color c = _silenced ? OrangeWarn : RedAlert;
                _iconCtrl.Symbol      = "🔋";
                _iconCtrl.SymbolColor = c;
                _iconCtrl.CircleColor = Color.FromArgb(248, 225, 225);
                _statusLabel.Text     = _silenced ? "UNPLUGGED (SILENCED)" : "CHARGER DISCONNECTED";
                _statusLabel.ForeColor = c;
                _barFg.BackColor      = c;
                _trayIcon.Text        = "Charger Removal Alarm — UNPLUGGED!";
            }
        }

        private void StartAlarm()
        {
            _alarmActive = true; _silenced = false;
            _silenceBtn.Enabled = true;
            _silenceBtn.BackColor = RedAlert;
            _silenceBtn.HoverColor = Color.FromArgb(210, 50, 50);
            _silenceBtn.Invalidate();
            _alarmCts = new CancellationTokenSource();
            var tok = _alarmCts.Token;
            Task.Run(() => {
                var wav = AlarmAudio.GenerateWav(2.5);
                while (!tok.IsCancellationRequested)
                { using var ms = new System.IO.MemoryStream(wav); using var pl = new SoundPlayer(ms); pl.PlaySync(); }
            }, tok);
            _flashTimer.Start();
            _trayIcon.ShowBalloonTip(3000, "⚠ Charger Removed!", "Your charger has been disconnected!", ToolTipIcon.Warning);
        }

        private void StopAlarm()
        {
            if (!_alarmActive) return;
            _alarmCts?.Cancel(); _alarmActive = false;
            _silenceBtn.Enabled = false;
            _silenceBtn.BackColor = Purple;
            _silenceBtn.HoverColor = PurpleLight;
            _silenceBtn.Invalidate();
            _flashTimer.Stop();
            this.BackColor = BgPage;
            _header.BackColor = Purple;
            _headerTitle.BackColor = Purple;
            _headerSub.BackColor = Purple;
        }

        private void SilenceAlarm()
        {
            _silenced = true;
            StopAlarm();
            UpdateDisplay(false, PowerHelper.GetStatus().percent);
        }

        private void OnFlashTick(object sender, EventArgs e)
        {
            _flashState = !_flashState;
            Color f = _flashState ? Color.FromArgb(220, 50, 50) : Color.FromArgb(180, 30, 30);
            _header.BackColor = _headerTitle.BackColor = _headerSub.BackColor = f;
        }

        private void ToggleMonitoring()
        {
            _monitoring = !_monitoring;
            if (_monitoring)
            {
                _toggleBtn.Text = "⏸  PAUSE MONITORING";
                _monitoringDot.ForeColor = GreenOk;
                _monitoringLabel.Text = "Monitoring active";
            }
            else
            {
                StopAlarm();
                _toggleBtn.Text = "▶  RESUME MONITORING";
                _monitoringDot.ForeColor = TextGray;
                _monitoringLabel.Text = "Monitoring paused";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _trayIcon.Visible = false;
            base.OnFormClosing(e);
        }
    }
}
