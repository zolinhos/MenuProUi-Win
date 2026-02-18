namespace MenuProUI.Models;

public class AppPreferences
{
    public string Theme { get; set; } = "Dark";
    public string ClientsIcon { get; set; } = "\uE716";
    public string AccessesIcon { get; set; } = "\uE7F4";
    public bool CompactAccessRows { get; set; }
    public int ConnectivityTimeoutMs { get; set; } = 3000;
    public int ConnectivityMaxConcurrency { get; set; } = 24;
    public string UrlFallbackPortsCsv { get; set; } = "443,80,8443,8080,9443";
    public bool RdpForceFullscreen { get; set; } = true;
}
