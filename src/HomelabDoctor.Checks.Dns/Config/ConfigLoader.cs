namespace HomelabDoctor.Checks.Dns.Config;

// Minimal YAML parser for our specific config shape. Avoids a YAML library dependency.
// Handles the homelab-doctor.yml structure documented in docs/examples/.
public static class ConfigLoader
{
    private static readonly string[] SearchPaths =
    [
        "homelab-doctor.yml",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "homelab-doctor", "homelab-doctor.yml")
    ];

    public static HomelabDoctorConfig? TryLoad()
    {
        foreach (var path in SearchPaths)
        {
            if (!File.Exists(path)) continue;
            try { return Parse(File.ReadAllText(path)); }
            catch { return null; }
        }
        return null;
    }

    internal static HomelabDoctorConfig? Parse(string yaml)
    {
        var config = new HomelabDoctorConfig();
        var lines = yaml.Split('\n');

        var inDns = false;
        var inRecords = false;
        ExpectedDnsRecord? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var indent = line.Length - line.TrimStart().Length;
            var trimmed = line.TrimStart();

            if (indent == 0 && trimmed.StartsWith("dns:"))
            {
                inDns = true;
                inRecords = false;
                config.Dns = new DnsConfig();
                continue;
            }

            if (!inDns) continue;

            if (indent == 2 && trimmed.StartsWith("records:"))
            {
                inRecords = true;
                continue;
            }

            if (!inRecords) continue;

            if (indent == 4 && trimmed.StartsWith("- "))
            {
                if (current is not null) config.Dns!.Records.Add(current);
                current = null;

                var kvp = ParseKv(trimmed[2..]);
                if (kvp is var (k, v)) current = ApplyField(current, k, v);
                continue;
            }

            if (indent == 6 && current is not null)
            {
                var kvp = ParseKv(trimmed);
                if (kvp is var (k, v)) current = ApplyField(current, k, v);
            }
        }

        if (current is not null) config.Dns!.Records.Add(current);

        return config.Dns?.Records.Count > 0 ? config : null;
    }

    private static ExpectedDnsRecord? ApplyField(ExpectedDnsRecord? record, string key, string value)
    {
        switch (key)
        {
            case "hostname":
                record = new ExpectedDnsRecord { Hostname = value, Expected = record?.Expected ?? "" };
                break;
            case "type" when record is not null:
                record.Type = value;
                break;
            case "expected" when record is not null:
                record.Expected = value;
                break;
        }
        return record;
    }

    private static (string key, string value)? ParseKv(string s)
    {
        var colon = s.IndexOf(':');
        if (colon < 0) return null;
        var key = s[..colon].Trim();
        var value = s[(colon + 1)..].Trim().Trim('"', '\'');
        return (key, value);
    }
}
