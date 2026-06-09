using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using HomelabDoctor.Checks.Traefik.Models;

namespace HomelabDoctor.Checks.Traefik;

public sealed class TlsInspector : ITlsInspector
{
    public async Task<TlsCertInfo> CheckAsync(string hostname, int port = 443, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(hostname, port, cts.Token);

            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = hostname,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }, cts.Token);

            DateTime expiry;
            if (ssl.RemoteCertificate is X509Certificate2 cert2)
            {
                expiry = cert2.NotAfter;
            }
            else if (ssl.RemoteCertificate is not null)
            {
                expiry = DateTime.Parse(ssl.RemoteCertificate.GetExpirationDateString());
            }
            else
            {
                return new TlsCertInfo { Hostname = hostname, Connected = true, Error = "No certificate returned" };
            }

            return new TlsCertInfo { Hostname = hostname, Connected = true, ExpiresAt = expiry.ToUniversalTime() };
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException
                                       or OperationCanceledException or TimeoutException)
        {
            return new TlsCertInfo { Hostname = hostname, Connected = false, Error = ex.Message };
        }
    }
}
