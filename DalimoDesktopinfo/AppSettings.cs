using System.Runtime.Serialization;

namespace DalimoDesktopinfo
{
    [DataContract]
    public class AppSettings
    {
        // Фон
        [DataMember] public string BackgroundColor { get; set; } = "#000000";
        [DataMember] public int BackgroundOpacity { get; set; } = 60;

        // Host
        [DataMember] public double HostFontSize { get; set; } = 14;
        [DataMember] public string HostColor { get; set; } = "#FFFFFF";

        // User
        [DataMember] public double UserFontSize { get; set; } = 12;
        [DataMember] public string UserColor { get; set; } = "#EECC00";

        // Uptime
        [DataMember] public double UptimeFontSize { get; set; } = 12;
        [DataMember] public string UptimeColor { get; set; } = "#00FFCC";

        // Network Name
        [DataMember] public double NameFontSize { get; set; } = 11;
        [DataMember] public string NameColor { get; set; } = "#55CC77";

        // Network Mac
        [DataMember] public double MacFontSize { get; set; } = 11;
        [DataMember] public string MacColor { get; set; } = "#AAAAAA";

        // Network Ip
        [DataMember] public double IpFontSize { get; set; } = 11;
        [DataMember] public string IpColor { get; set; } = "#00FFFF";
    }
}