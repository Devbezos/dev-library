using DevClient.Data;
using Microsoft.Playwright;
using System.Net;

namespace DevClient.Clients;

public static class ScrapingNetwork
{
    public static HttpClient CreateHttpClient(bool useCookies = true)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = useCookies,
        };

        ApplyExpressVpnProxy(handler);
        return new HttpClient(handler);
    }

    public static void ApplyExpressVpnProxy(HttpClientHandler handler)
    {
        var proxy = CreateProxy();
        if (proxy == null)
            return;

        handler.Proxy = proxy;
        handler.UseProxy = true;
    }

    public static Proxy? CreatePlaywrightProxy()
    {
        if (!AppSettings.ExpressVpn.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.ProxyServer))
            return null;

        return new Proxy
        {
            Server = AppSettings.ExpressVpn.ProxyServer,
            Bypass = AppSettings.ExpressVpn.ProxyBypass,
            Username = string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.Username) ? null : AppSettings.ExpressVpn.Username,
            Password = string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.Password) ? null : AppSettings.ExpressVpn.Password,
        };
    }

    private static IWebProxy? CreateProxy()
    {
        if (!AppSettings.ExpressVpn.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.ProxyServer))
            return null;

        var proxy = new WebProxy(AppSettings.ExpressVpn.ProxyServer)
        {
            BypassProxyOnLocal = AppSettings.ExpressVpn.BypassProxyOnLocal,
        };

        if (!string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.ProxyBypass))
            proxy.BypassList = AppSettings.ExpressVpn.ProxyBypass.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!string.IsNullOrWhiteSpace(AppSettings.ExpressVpn.Username))
        {
            proxy.Credentials = new NetworkCredential(
                AppSettings.ExpressVpn.Username,
                AppSettings.ExpressVpn.Password);
        }

        return proxy;
    }
}
