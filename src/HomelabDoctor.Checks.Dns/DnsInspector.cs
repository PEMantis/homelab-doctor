using System.Net;
using System.Net.Sockets;
using HomelabDoctor.Checks.Dns.Models;
using HomelabDoctor.Core.Interfaces;

namespace HomelabDoctor.Checks.Dns;

public sealed class DnsInspector : IDnsInspector
{
    private readonly ICommandRunner _runner;

    public DnsInspector(ICommandRunner runner) => _runner = runner;

    public async Task<IReadOnlyList<string>> GetConfiguredNameserversAsync(CancellationToken ct = default)
    {
        var servers = new List<string>();

        // Linux / macOS: parse /etc/resolv.conf
        const string resolvConf = "/etc/resolv.conf";
        if (File.Exists(resolvConf))
        {
            foreach (var line in await File.ReadAllLinesAsync(resolvConf, ct))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase)) continue;
                var server = trimmed["nameserver ".Length..].Trim();
                if (!string.IsNullOrEmpty(server))
                    servers.Add(server);
            }
        }

        // Fallback: try systemd-resolve if /etc/resolv.conf gave nothing useful
        if (servers.Count == 0 || servers.All(s => s is "127.0.0.53" or "127.0.0.1"))
        {
            var output = await _runner.RunAsync("systemd-resolve", "--status --no-pager", ct);
            if (output.ExitCode == 0)
            {
                foreach (var line in output.Stdout.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("DNS Servers:", StringComparison.OrdinalIgnoreCase)) continue;
                    var server = trimmed["DNS Servers:".Length..].Trim();
                    if (!string.IsNullOrEmpty(server) && !servers.Contains(server))
                        servers.Add(server);
                }
            }
        }

        return servers;
    }

    public async Task<DnsResolutionResult> ResolveLocalAsync(string hostname, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var addresses = await System.Net.Dns.GetHostAddressesAsync(hostname, cts.Token);
            var ipv4 = addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();
            var all = ipv4.Count > 0 ? ipv4 : addresses.Select(a => a.ToString()).ToList();

            return new DnsResolutionResult
            {
                Hostname = hostname,
                Resolved = all.Count > 0,
                Addresses = all
            };
        }
        catch (SocketException ex)
        {
            return new DnsResolutionResult { Hostname = hostname, Resolved = false, Error = ex.Message };
        }
        catch (OperationCanceledException)
        {
            return new DnsResolutionResult { Hostname = hostname, Resolved = false, Error = "Resolution timed out" };
        }
    }

    public async Task<DnsResolutionResult> ResolveViaServerAsync(string hostname, string server, CancellationToken ct = default)
    {
        // dig @server hostname A +short +time=3 +tries=1
        var output = await _runner.RunAsync("dig", $"@{server} {hostname} A +short +time=3 +tries=1", ct);

        if (output.ExitCode != 0)
            return new DnsResolutionResult
            {
                Hostname = hostname,
                Resolved = false,
                QueryServer = server,
                Error = output.Stderr.Trim()
            };

        var addresses = output.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => IPAddress.TryParse(line, out _))
            .ToList();

        return new DnsResolutionResult
        {
            Hostname = hostname,
            Resolved = addresses.Count > 0,
            Addresses = addresses,
            QueryServer = server
        };
    }

    public async Task<bool> IsDigAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await _runner.RunAsync("dig", "-v", ct);
            return output.ExitCode == 0 || output.Stderr.Contains("DiG");
        }
        catch { return false; }
    }
}
