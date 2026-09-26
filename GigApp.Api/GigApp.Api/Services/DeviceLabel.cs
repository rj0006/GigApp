namespace GigApp.Api.Services
{
    public static class DeviceLabel
    {
        public static string FromUserAgent(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

            var os = userAgent switch
            {
                var s when s.Contains("Windows") => "Windows",
                var s when s.Contains("iPhone") => "iPhone",
                var s when s.Contains("iPad") => "iPad",
                var s when s.Contains("Mac OS") => "Mac",
                var s when s.Contains("Android") => "Android",
                var s when s.Contains("Linux") => "Linux",
                _ => null,
            };

            var browser = userAgent switch
            {
                var s when s.Contains("Edg/") => "Edge",
                var s when s.Contains("OPR/") || s.Contains("Opera") => "Opera",
                var s when s.Contains("Chrome/") => "Chrome",
                var s when s.Contains("CriOS") => "Chrome",
                var s when s.Contains("Firefox") => "Firefox",
                var s when s.Contains("Safari") && !s.Contains("Chrome") => "Safari",
                _ => null,
            };

            return (browser, os) switch
            {
                (null, null) => "Unknown device",
                (not null, null) => browser!,
                (null, not null) => os!,
                _ => $"{browser} on {os}",
            };
        }
    }
}
