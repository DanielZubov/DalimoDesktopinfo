using System.Windows;
using System.Windows.Controls;

namespace DalimoDesktopinfo
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // 1. Сначала подписываемся на событие, чтобы текст обновлялся
            SliderOpacity.ValueChanged += (sender, e) =>
            {
                TxtOpacityValue.Text = $"{(int)SliderOpacity.Value}%";
            };

            // Загружаем текущие настройки
            var s = SettingsManager.Load();

            TxtHostSize.Text = s.HostFontSize.ToString();
            TxtHostColor.Text = s.HostColor;

            TxtUserSize.Text = s.UserFontSize.ToString();
            TxtUserColor.Text = s.UserColor;

            TxtUptimeSize.Text = s.UptimeFontSize.ToString();
            TxtUptimeColor.Text = s.UptimeColor;

            TxtNameSize.Text = s.NameFontSize.ToString();
            TxtNameColor.Text = s.NameColor;

            TxtMacSize.Text = s.MacFontSize.ToString();
            TxtMacColor.Text = s.MacColor;

            TxtIpSize.Text = s.IpFontSize.ToString();
            TxtIpColor.Text = s.IpColor;

            TxtBgColor.Text = s.BackgroundColor;

            // 2. Теперь присваиваем значение. Событие сработает и обновит текст!
            SliderOpacity.Value = s.BackgroundOpacity;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = new AppSettings
                {
                    HostFontSize = double.Parse(TxtHostSize.Text),
                    HostColor = TxtHostColor.Text,
                    UserFontSize = double.Parse(TxtUserSize.Text),
                    UserColor = TxtUserColor.Text,
                    UptimeFontSize = double.Parse(TxtUptimeSize.Text),
                    UptimeColor = TxtUptimeColor.Text,
                    NameFontSize = double.Parse(TxtNameSize.Text),
                    NameColor = TxtNameColor.Text,
                    MacFontSize = double.Parse(TxtMacSize.Text),
                    MacColor = TxtMacColor.Text,
                    IpFontSize = double.Parse(TxtIpSize.Text),
                    IpColor = TxtIpColor.Text,
                    BackgroundColor = TxtBgColor.Text,
                    BackgroundOpacity = (int)SliderOpacity.Value
                };

                SettingsManager.Save(s);
                this.DialogResult = true;
                this.Close();
            }
            catch
            {
                MessageBox.Show("Проверьте правильность ввода чисел и HEX-кодов (например, #FFFFFF).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}