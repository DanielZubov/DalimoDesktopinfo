using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms; // Для трея
using Drawing = System.Drawing; // Чтобы не было конфликта имен с WPF

namespace DalimoDesktopinfo
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _updateTimer;
        private DispatcherTimer _autoResetTimer; // Таймер для возврата окна
        private DispatcherTimer _toastTimer;
        private DateTime _bootTime;
        private string _diagnosticResults = "Диагностика ещё не запускалась.";
        private bool _diagnosticsRunning;

        private NotifyIcon _notifyIcon; // Иконка в трее

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _bootTime = DateTime.Now.AddMilliseconds(-Environment.TickCount);

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _toastTimer.Tick += (s, e) =>
            {
                ToastPanel.Visibility = Visibility.Collapsed;
                _toastTimer.Stop();
            };

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

            var diagnosticsItem = new ToolStripMenuItem("Диагностика");
            diagnosticsItem.Click += (s, e) => Dispatcher.BeginInvoke(new Action(() => RunDiagnosticsAsync()));

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
                System.Windows.MessageBox.Show("Dalimo Desktop Info\n\nАвтор: ZubovDS by MadLab\nВерсия: 1.2\n\n© 2026", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
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
            contextMenu.Items.Add(diagnosticsItem);
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
        private async void MenuDiagnostics_Click(object sender, RoutedEventArgs e) => await RunDiagnosticsAsync();
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

        private void CopyText_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            var text = (border?.Child as System.Windows.Controls.TextBlock ?? sender as System.Windows.Controls.TextBlock)?.Text;
            if (!string.IsNullOrWhiteSpace(text)) CopyToClipboard(text, "Скопировано");
            e.Handled = true;
        }

        private void ClickableText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateClickableText(sender as System.Windows.Controls.Border, true);
        }

        private void ClickableText_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateClickableText(sender as System.Windows.Controls.Border, false);
        }

        private static void AnimateClickableText(System.Windows.Controls.Border border, bool isHovered)
        {
            if (border == null) return;

            var brush = border.Background as SolidColorBrush;
            if (brush == null || brush.IsFrozen)
            {
                brush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                border.Background = brush;
            }

            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
            {
                To = isHovered ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(0, 255, 255, 255),
                Duration = TimeSpan.FromMilliseconds(180)
            });

            var scale = border.RenderTransform as ScaleTransform;
            if (scale != null)
            {
                if (scale.IsFrozen)
                {
                    scale = scale.CloneCurrentValue();
                    border.RenderTransform = scale;
                }

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    To = isHovered ? 1.04 : 1.0,
                    Duration = TimeSpan.FromMilliseconds(140)
                });
            }
        }

        private void Status_Click(object sender, MouseButtonEventArgs e)
        {
            ShowDiagnosticResults();
            e.Handled = true;
        }

        private void CopyToClipboard(string text, string notification)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                ShowToast(notification);
            }
            catch
            {
                ShowToast("Не удалось скопировать");
            }
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastPanel.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void ShowDiagnosticResults()
        {
            var resultWindow = new Window
            {
                Title = "Результаты диагностики",
                Width = 480,
                Height = 320,
                MinWidth = 380,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize
            };

            var layout = new System.Windows.Controls.DockPanel { Margin = new Thickness(10) };
            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);

            var copyButton = new System.Windows.Controls.Button { Content = "Скопировать", Width = 110, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6, 3, 6, 3) };
            copyButton.Click += (s, e) => CopyToClipboard(_diagnosticResults, "Готово");
            var closeButton = new System.Windows.Controls.Button { Content = "Закрыть", Width = 80, Padding = new Thickness(6, 3, 6, 3) };
            closeButton.Click += (s, e) => resultWindow.Close();
            buttons.Children.Add(copyButton);
            buttons.Children.Add(closeButton);

            var resultText = new System.Windows.Controls.TextBox
            {
                Text = _diagnosticResults,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas")
            };
            layout.Children.Add(buttons);
            layout.Children.Add(resultText);
            resultWindow.Content = layout;
            resultWindow.ShowDialog();
        }

        private async void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            await RunDiagnosticsAsync();
        }

        private async Task RunDiagnosticsAsync()
        {
            if (_diagnosticsRunning) return;

            _diagnosticsRunning = true;
            DiagnosticsButton.IsEnabled = false;
            DiagnosticsButton.Content = "Проверка...";
            StatusIndicator.Fill = Brushes.Gray;

            try
            {
                var report = await Task.Run(() => RunDiagnostics(GetDefaultGatewayAddress()));
                _diagnosticResults = report.Item1;
                StatusIndicator.Fill = report.Item2 ? Brushes.LimeGreen : Brushes.Red;
                CopyToClipboard(_diagnosticResults, "Готово");
            }
            catch (Exception ex)
            {
                _diagnosticResults = "Не удалось выполнить диагностику: " + ex.Message;
                StatusIndicator.Fill = Brushes.Red;
                CopyToClipboard(_diagnosticResults, "Готово");
            }
            finally
            {
                DiagnosticsButton.Content = "Диагностика";
                DiagnosticsButton.IsEnabled = true;
                _diagnosticsRunning = false;
            }
        }

        private static string GetDefaultGatewayAddress()
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    var gateway = adapter.GetIPProperties().GatewayAddresses
                        .Select(item => item.Address)
                        .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(address));

                    if (gateway != null) return gateway.ToString();
                }
                catch (NetworkInformationException)
                {
                }
            }

            return null;
        }

        private Tuple<string, bool> RunDiagnostics(string gateway)
        {
            var output = new StringBuilder();
            bool allPassed = true;

            output.AppendLine("Результаты сетевой диагностики");
            output.AppendLine("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            output.AppendLine();

            CheckPing(output, "77.88.8.8", "77.88.8.8", ref allPassed);
            CheckPing(output, "10.0.150.8", "10.0.150.8", ref allPassed);
            CheckPing(output, "192.168.120.8", "192.168.120.8", ref allPassed);

            if (string.IsNullOrWhiteSpace(gateway) || !IPAddress.TryParse(gateway.Trim(), out _))
            {
                output.AppendLine("Шлюз: ОШИБКА — IPv4-шлюз не найден в конфигурации сети Windows");
                allPassed = false;
            }
            else
            {
                CheckPing(output, "Шлюз (" + gateway.Trim() + ")", gateway.Trim(), ref allPassed);
            }

            try
            {
                var addresses = Dns.GetHostAddresses("dalimo.ru");
                if (addresses.Length == 0)
                {
                    output.AppendLine("DNS dalimo.ru: ОШИБКА — адреса не найдены");
                    allPassed = false;
                }
                else
                {
                    output.AppendLine("DNS dalimo.ru: УСПЕШНО — " + string.Join(", ", addresses.Select(address => address.ToString())));
                }
            }
            catch (Exception ex)
            {
                output.AppendLine("DNS dalimo.ru: ОШИБКА — " + ex.Message);
                allPassed = false;
            }

            return Tuple.Create(output.ToString().TrimEnd(), allPassed);
        }

        private static void CheckPing(StringBuilder output, string label, string address, ref bool allPassed)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(address, 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        output.AppendLine(label + ": УСПЕШНО — ответ от " + reply.Address + ", время " + reply.RoundtripTime + " мс");
                    }
                    else
                    {
                        output.AppendLine(label + ": ОШИБКА — " + reply.Status);
                        allPassed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                output.AppendLine(label + ": ОШИБКА — " + ex.Message);
                allPassed = false;
            }
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