using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms; // Для трея
using Drawing = System.Drawing; // Чтобы не было конфликта имен с WPF

namespace DalimoDesktopinfo
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _updateTimer;
        private DispatcherTimer _autoResetTimer; // Таймер для возврата окна
        private DateTime _bootTime;

        private NotifyIcon _notifyIcon; // Иконка в трее

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _bootTime = DateTime.Now.AddMilliseconds(-Environment.TickCount);

            // Таймер обновления данных (каждые 5 секунд)
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(5);
            _updateTimer.Tick += (s, e) => UpdateData();
            _updateTimer.Start();

            // Таймер возврата окна на место (каждую минуту)
            _autoResetTimer = new DispatcherTimer();
            _autoResetTimer.Interval = TimeSpan.FromMinutes(1);
            _autoResetTimer.Tick += (s, e) => ResetWindowPosition();
            _autoResetTimer.Start();

            InitializeTrayIcon();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            ResetWindowPosition();
        }

        // --- Логика иконки в трее ---
        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();

            // Загружаем иконку прямо из нашего .exe файла
            try
            {
                _notifyIcon.Icon = Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                _notifyIcon.Icon = Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "Dalimo Desktop Info";
            _notifyIcon.Visible = true;

            // Создаем контекстное меню для трея (точно такое же, как у виджета)
            var contextMenu = new ContextMenuStrip();

            var refreshItem = new ToolStripMenuItem("Обновить");
            refreshItem.Click += (s, e) => UpdateData();

            var resetItem = new ToolStripMenuItem("Восстановить положение");
            resetItem.Click += (s, e) => ResetWindowPosition();

            var settingsItem = new ToolStripMenuItem("Настройки");
            settingsItem.Click += (s, e) =>
            {
                var sw = new SettingsWindow { Owner = this };
                if (sw.ShowDialog() == true) ApplySettings();
            };

            var aboutItem = new ToolStripMenuItem("О программе");
            aboutItem.Click += (s, e) =>
            {
                System.Windows.MessageBox.Show("Dalimo Desktop Info\n\nАвтор: ZubovDS by Dalimo\nВерсия: 1.2\n\n© 2026", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            var exitItem = new ToolStripMenuItem("Выход");
            exitItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                // Разрешаем приложению закрыться
                System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                System.Windows.Application.Current.Shutdown();
            };

            // Собираем меню
            contextMenu.Items.Add(refreshItem);
            contextMenu.Items.Add(resetItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(aboutItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Двойной клик по иконке показывает/скрывает виджет
            _notifyIcon.DoubleClick += (s, e) =>
            {
                if (this.IsVisible) this.Hide();
                else this.Show();
            };
        }

        // Обработка закрытия окна
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Если приложение закрывается по команде "Выход", просто скрываем иконку
            if (System.Windows.Application.Current.ShutdownMode == ShutdownMode.OnMainWindowClose)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                return;
            }

            // В остальных случаях (например, Alt+F4) скрываем окно в трей
            e.Cancel = true;
            this.Hide();
        }

        // --- Логика отображения ---

        public void ApplySettings()
        {
            var s = SettingsManager.Load();

            try
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(s.BackgroundColor);
                byte alpha = (byte)(s.BackgroundOpacity * 255 / 100);
                brush.Color = Color.FromArgb(alpha, brush.Color.R, brush.Color.G, brush.Color.B);
                MainBorder.Background = brush;
            }
            catch { }

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

            UpdateData();
        }

        private void ResetWindowPosition()
        {
            this.Left = SystemParameters.WorkArea.Width - this.ActualWidth - 10;
            this.Top = 10;
        }

        // --- Обработчики меню виджета ---
        private void MenuRefresh_Click(object sender, RoutedEventArgs e) => UpdateData();
        private void MenuResetPosition_Click(object sender, RoutedEventArgs e) => ResetWindowPosition();

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var sw = new SettingsWindow { Owner = this };
            if (sw.ShowDialog() == true) ApplySettings();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Dalimo Desktop Info\n\nАвтор: ZubovDS by Dalimo\nВерсия: 1.2\n\n© 2024", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            System.Windows.Application.Current.Shutdown();
        }

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

                Brush safeNameColor = Brushes.LightGreen;
                Brush safeMacColor = Brushes.LightGray;
                Brush safeIpColor = Brushes.Cyan;

                if (!string.IsNullOrWhiteSpace(s.NameColor))
                    try { safeNameColor = (SolidColorBrush)new BrushConverter().ConvertFromString(s.NameColor); } catch { }

                if (!string.IsNullOrWhiteSpace(s.MacColor))
                    try { safeMacColor = (SolidColorBrush)new BrushConverter().ConvertFromString(s.MacColor); } catch { }

                if (!string.IsNullOrWhiteSpace(s.IpColor))
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