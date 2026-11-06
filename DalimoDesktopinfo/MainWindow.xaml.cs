using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DalimoDesktopinfo
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private DateTime _bootTime;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            _bootTime = DateTime.Now.AddMilliseconds(-Environment.TickCount);
            UpdateData();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += (s, e) => UpdateData();
            _timer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            ResetWindowPosition();
        }

        public void ApplySettings()
        {
            var s = SettingsManager.Load();

            try
            {
                // Настройка фона с прозрачностью
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(s.BackgroundColor);
                byte alpha = (byte)(s.BackgroundOpacity * 255 / 100);
                brush.Color = Color.FromArgb(alpha, brush.Color.R, brush.Color.G, brush.Color.B);
                MainBorder.Background = brush;
            }
            catch { }

            // Настройка текста (с защитой от пустых значений)
            TxtHost.FontSize = s.HostFontSize;
            if (!string.IsNullOrWhiteSpace(s.HostColor))
            {
                try { TxtHost.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(s.HostColor); }
                catch { TxtHost.Foreground = Brushes.White; }
            }

            TxtUser.FontSize = s.UserFontSize;
            if (!string.IsNullOrWhiteSpace(s.UserColor))
            {
                try { TxtUser.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(s.UserColor); }
                catch { TxtUser.Foreground = Brushes.Yellow; }
            }

            TxtUptime.FontSize = s.UptimeFontSize;
            if (!string.IsNullOrWhiteSpace(s.UptimeColor))
            {
                try { TxtUptime.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(s.UptimeColor); }
                catch { TxtUptime.Foreground = Brushes.Cyan; }
            }

            // Обновляем сетевые адаптеры, чтобы применить к ним настройки
            UpdateData();
        }

        private void ResetWindowPosition()
        {
            this.Left = SystemParameters.WorkArea.Width - this.ActualWidth - 10;
            this.Top = 10;
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e) => UpdateData();
        private void MenuResetPosition_Click(object sender, RoutedEventArgs e) => ResetWindowPosition();

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow { Owner = this };
            if (sw.ShowDialog() == true) ApplySettings();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("DalimoDesktopinfo\n\nАвтор: ZubovDS by MadLab\nВерсия: 1.1\n\n© 2026", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void UpdateData()
        {
            TxtHost.Text = Environment.MachineName;
            TxtUser.Text = Environment.UserName;

            TimeSpan uptime = DateTime.Now - _bootTime;
            TxtUptime.Text = $"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";

            var s = SettingsManager.Load();
            var adapters = new List<NetworkInfo>();

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.Description.Contains("Virtual") || ni.Description.Contains("Bluetooth")) continue;

                string mac = ni.GetPhysicalAddress().ToString();
                if (string.IsNullOrEmpty(mac)) continue;
                mac = string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));

                string ip = "";
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork) { ip = addr.Address.ToString(); break; }
                }

                if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0") continue;

                // Безопасная конвертация цветов (если в настройках мусор, используем дефолт)
                Brush safeNameColor = Brushes.LightGreen;
                Brush safeMacColor = Brushes.LightGray;
                Brush safeIpColor = Brushes.Cyan;

                try { safeNameColor = (SolidColorBrush)new BrushConverter().ConvertFromString(s.NameColor); } catch { }
                try { safeMacColor = (SolidColorBrush)new BrushConverter().ConvertFromString(s.MacColor); } catch { }
                try { safeIpColor = (SolidColorBrush)new BrushConverter().ConvertFromString(s.IpColor); } catch { }

                adapters.Add(new NetworkInfo
                {
                    Name = ni.Name,
                    Mac = mac,
                    Ip = ip,
                    NameSize = s.NameFontSize,
                    NameColor = safeNameColor,
                    MacSize = s.MacFontSize,
                    MacColor = safeMacColor,
                    IpSize = s.IpFontSize,
                    IpColor = safeIpColor
                });
            }
            NetworkList.ItemsSource = adapters;
        }
    }

    public class NetworkInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public double NameSize { get; set; }
        public Brush NameColor { get; set; }
        public double MacSize { get; set; }
        public Brush MacColor { get; set; }
        public double IpSize { get; set; }
        public Brush IpColor { get; set; }
    }
}