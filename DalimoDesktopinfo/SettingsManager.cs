using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DalimoDesktopinfo
{
    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    using (var fs = new FileStream(SettingsPath, FileMode.Open))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                        return (AppSettings)serializer.ReadObject(fs);
                    }
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            using (var fs = new FileStream(SettingsPath, FileMode.Create))
            {
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(fs, settings);
            }
        }
    }
}