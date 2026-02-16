using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using MenuProUI.Models;

namespace MenuProUI.Services;

public static class ConnectivityChecker
{
    public static async Task<Dictionary<Guid, bool>> CheckAllAsync(IEnumerable<AccessEntry> accesses, int timeoutMs = 3000)
    {
        var list = accesses.ToList();
        var tasks = list.Select(async access =>
        {
            var ok = await CheckAsync(access, timeoutMs);
            return (access.Id, ok);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(x => x.Id, x => x.ok);
    }

    public static async Task<bool> CheckAsync(AccessEntry access, int timeoutMs = 3000)
    {
        var (host, port) = ResolveHostPort(access);
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            return false;

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));
            if (completed != connectTask)
                return false;

            await connectTask;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static (string? host, int port) ResolveHostPort(AccessEntry access)
    {
        switch (access.Tipo)
        {
            case AccessType.SSH:
                return (access.Host, access.Porta is > 0 ? access.Porta.Value : 22);

            case AccessType.RDP:
                return (access.Host, access.Porta is > 0 ? access.Porta.Value : 3389);

            case AccessType.URL:
                {
                    var raw = (access.Url ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(raw))
                        return (null, 0);

                    var candidate = raw.Contains("://", StringComparison.Ordinal) ? raw : $"https://{raw}";
                    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                        return (null, 0);

                    var resolvedPort = uri.Port > 0 ? uri.Port : (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);
                    return (uri.Host, resolvedPort);
                }

            default:
                return (null, 0);
        }
    }
}
