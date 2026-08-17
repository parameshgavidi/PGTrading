using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PgAiTrading.Services;

/// <summary>
/// Resolves the machine's outbound/public IP for Auto Buy diagnostics
/// (Zerodha API IP whitelist) and failed-entry records.
/// </summary>
public interface IPublicIpAddressService
{
    /// <summary>Last successfully resolved address (may be empty until first refresh).</summary>
    string? CurrentIpAddress { get; }

    Task<string?> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class PublicIpAddressService : IPublicIpAddressService, IDisposable
{
    private static readonly Uri[] PublicIpEndpoints =
    [
        new("https://api.ipify.org"),
        new("https://icanhazip.com"),
        new("https://ifconfig.me/ip")
    ];

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public string? CurrentIpAddress { get; private set; }

    public PublicIpAddressService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public async Task<string?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (var endpoint in PublicIpEndpoints)
            {
                try
                {
                    using var response = await _http.GetAsync(endpoint, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                    if (TryNormalizeIp(text, out var ip))
                    {
                        CurrentIpAddress = ip;
                        return CurrentIpAddress;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Try next endpoint / local fallback.
                }
            }

            var local = TryGetLocalIPv4();
            if (!string.IsNullOrWhiteSpace(local))
                CurrentIpAddress = local;

            return CurrentIpAddress;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool TryNormalizeIp(string? text, out string ip)
    {
        ip = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var candidate = text.Trim().Split('\n', '\r', ' ')[0].Trim();
        if (!IPAddress.TryParse(candidate, out var parsed))
            return false;

        // Prefer IPv4 for Zerodha whitelist display; still accept IPv6 if that is all we get.
        ip = parsed.ToString();
        return true;
    }

    private static string? TryGetLocalIPv4()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IPAddress.IsLoopback(addr.Address))
                        continue;

                    return addr.Address.ToString();
                }
            }
        }
        catch
        {
            // Ignore — IP display is best-effort.
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _http.Dispose();
        _lock.Dispose();
        _disposed = true;
    }
}
