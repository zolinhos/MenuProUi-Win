using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MenuProUI.Models;

namespace MenuProUI.Services;

public static class ConnectivityChecker
{
    private static readonly int[] DefaultUrlFallbackPorts = { 443, 80, 8443, 8080, 9443 };

    public static async Task<Dictionary<Guid, bool>> CheckAllAsync(
        IEnumerable<AccessEntry> accesses,
        int timeoutMs = 3000,
        int maxConcurrency = 24,
        IReadOnlyList<int>? urlFallbackPorts = null)
    {
        var list = accesses.ToList();
        var endpointToAccessIds = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        var endpointToProbe = new Dictionary<string, (string host, int port)>(StringComparer.OrdinalIgnoreCase);

        foreach (var access in list)
        {
            foreach (var endpoint in ResolveHostPorts(access, urlFallbackPorts))
            {
                var key = BuildEndpointKey(endpoint.host, endpoint.port);
                if (!endpointToAccessIds.TryGetValue(key, out var ids))
                {
                    ids = new List<Guid>();
                    endpointToAccessIds[key] = ids;
                    endpointToProbe[key] = endpoint;
                }
                ids.Add(access.Id);
            }
        }

        var concurrency = Math.Clamp(maxConcurrency, 1, 128);
        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var probeTasks = endpointToProbe.Select(async pair =>
        {
            await gate.WaitAsync();
            try
            {
                var ok = await ProbeTcpAsync(pair.Value.host, pair.Value.port, timeoutMs);
                return (pair.Key, ok);
            }
            finally
            {
                gate.Release();
            }
        });

        var probeResults = await Task.WhenAll(probeTasks);
        var perAccess = list.ToDictionary(a => a.Id, _ => false);
        var probeMap = probeResults.ToDictionary(x => x.Key, x => x.ok, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in endpointToAccessIds)
        {
            if (!probeMap.TryGetValue(pair.Key, out var ok) || !ok) continue;
            foreach (var accessId in pair.Value)
                perAccess[accessId] = true;
        }

        return perAccess;
    }

    public static async Task<bool> CheckAsync(
        AccessEntry access,
        int timeoutMs = 3000,
        IReadOnlyList<int>? urlFallbackPorts = null)
    {
        foreach (var endpoint in ResolveHostPorts(access, urlFallbackPorts))
        {
            if (await ProbeTcpAsync(endpoint.host, endpoint.port, timeoutMs))
                return true;
        }

        return false;
    }

    private static async Task<bool> ProbeTcpAsync(string host, int port, int timeoutMs)
    {
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

    private static List<(string host, int port)> ResolveHostPorts(AccessEntry access, IReadOnlyList<int>? urlFallbackPorts)
    {
        switch (access.Tipo)
        {
            case AccessType.SSH:
                {
                    var host = (access.Host ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(host))
                        return [];

                    var port = access.Porta is > 0 ? access.Porta.Value : 22;
                    return [(host, port)];
                }

            case AccessType.RDP:
                {
                    var host = (access.Host ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(host))
                        return [];

                    var port = access.Porta is > 0 ? access.Porta.Value : 3389;
                    return [(host, port)];
                }

            case AccessType.URL:
                {
                    var raw = NormalizeUrlInput(access.Url ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(raw))
                        return [];

                    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
                        return [];

                    var defaultPort = DefaultPortForScheme(uri.Scheme);
                    var primaryPort = uri.Port > 0 ? uri.Port : defaultPort;

                    var ports = new List<int> { primaryPort };
                    if (uri.Port <= 0)
                    {
                        foreach (var fallback in (urlFallbackPorts is { Count: > 0 } ? urlFallbackPorts : DefaultUrlFallbackPorts))
                        {
                            if (!ports.Contains(fallback))
                                ports.Add(fallback);
                        }
                    }

                    return ports
                        .Where(p => p is > 0 and <= 65535)
                        .Select(p => (uri.Host, p))
                        .ToList();
                }

            default:
                return [];
        }
    }

    private static string NormalizeUrlInput(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (trimmed.Contains("://", StringComparison.Ordinal))
            return trimmed;

        // Igual ao MAC: sem esquema explícito, assume HTTP primeiro.
        return $"http://{trimmed}";
    }

    private static int DefaultPortForScheme(string? scheme) => (scheme ?? string.Empty).ToLowerInvariant() switch
    {
        "https" => 443,
        "ftp" => 21,
        _ => 80
    };

    private static string BuildEndpointKey(string host, int port) => $"{host.Trim().ToLowerInvariant()}:{port}";
}
