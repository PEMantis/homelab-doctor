using HomelabDoctor.Checks.Dns.Config;
using Xunit;

namespace HomelabDoctor.Checks.Dns.Tests;

public class ConfigLoaderTests
{
    private const string SampleYaml = """
        dns:
          records:
            - hostname: grafana.home.arpa
              type: A
              expected: 192.168.1.50
            - hostname: traefik.home.arpa
              type: A
              expected: 192.168.1.51
        """;

    [Fact]
    public void Parses_expected_dns_records_from_yaml()
    {
        var config = ConfigLoader.Parse(SampleYaml);

        Assert.NotNull(config);
        Assert.NotNull(config.Dns);
        Assert.Equal(2, config.Dns.Records.Count);

        Assert.Equal("grafana.home.arpa", config.Dns.Records[0].Hostname);
        Assert.Equal("192.168.1.50", config.Dns.Records[0].Expected);
        Assert.Equal("A", config.Dns.Records[0].Type);

        Assert.Equal("traefik.home.arpa", config.Dns.Records[1].Hostname);
        Assert.Equal("192.168.1.51", config.Dns.Records[1].Expected);
    }

    [Fact]
    public void Returns_null_for_empty_or_missing_content()
    {
        Assert.Null(ConfigLoader.Parse(""));
        Assert.Null(ConfigLoader.Parse("# just a comment\n"));
    }

    [Fact]
    public void Ignores_comment_lines_and_blank_lines()
    {
        var yaml = """
            # This is a comment
            dns:
              records:
                # another comment
                - hostname: pihole.home.arpa
                  expected: 192.168.1.2
            """;

        var config = ConfigLoader.Parse(yaml);

        Assert.NotNull(config);
        Assert.Single(config!.Dns!.Records);
        Assert.Equal("pihole.home.arpa", config.Dns.Records[0].Hostname);
    }
}
