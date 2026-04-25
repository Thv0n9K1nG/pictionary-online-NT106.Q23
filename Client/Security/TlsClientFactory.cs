using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Client.Security;

public sealed class TlsClientFactory
{
    public async Task<SslStream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken);

        var sslStream = new SslStream(
            tcpClient.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: CertificatePinningValidator.ValidateServerCertificate
        );

        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host
        }, cancellationToken);

        return sslStream;
    }
}
