namespace HomelabDoctor.Checks.Dns.Config;

public sealed class HomelabDoctorConfig
{
    public DnsConfig? Dns { get; set; }
}

public sealed class DnsConfig
{
    public List<ExpectedDnsRecord> Records { get; set; } = [];
}

public sealed class ExpectedDnsRecord
{
    public required string Hostname { get; set; }
    public string Type { get; set; } = "A";
    public required string Expected { get; set; }
}
