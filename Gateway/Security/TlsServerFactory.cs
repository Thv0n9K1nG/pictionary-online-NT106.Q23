using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Gateway.Security;

/// <summary>
/// Creates the client-facing TLS listener for Gateway.
///
/// Important Windows/SChannel note:
/// A certificate created only in memory uses an ephemeral private key. SslStream server mode on
/// Windows can reject that key with: "The platform does not support ephemeral keys".
/// Therefore this factory exports the dev certificate to PFX and re-imports it using PersistKeySet.
/// </summary>
public sealed class TlsServerFactory
{
    private const string DevCertificatePassword = "pictionary-dev-only";
    private const string DevCertificateFileName = "pictionary-gateway-dev.pfx";

    private readonly X509Certificate2 _certificate;

    public TlsServerFactory()
        : this(LoadOrCreateDevelopmentCertificate())
    {
    }

    public TlsServerFactory(X509Certificate2 certificate)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    }

    public TcpListener CreateListener(int port)
    {
        return new TcpListener(IPAddress.Any, port);
    }

    public async Task<SslStream> AuthenticateAsServerAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
    {
        var sslStream = new SslStream(
            tcpClient.GetStream(),
            leaveInnerStreamOpen: false);

        var options = new SslServerAuthenticationOptions
        {
            ServerCertificate = _certificate,
            ClientCertificateRequired = false,

            // TLS 1.2 is enough for the course demo and works reliably with PowerShell/OpenSSL tests.
            EnabledSslProtocols = SslProtocols.Tls12,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        };

        await sslStream.AuthenticateAsServerAsync(options, cancellationToken);
        return sslStream;
    }

    private static X509Certificate2 LoadOrCreateDevelopmentCertificate()
    {
        var certificateDirectory = Path.Combine(AppContext.BaseDirectory, "certs");
        var certificatePath = Path.Combine(certificateDirectory, DevCertificateFileName);

        Directory.CreateDirectory(certificateDirectory);

        if (!File.Exists(certificatePath))
        {
            using var createdCertificate = CreateDevelopmentCertificate();
            var pfxBytes = createdCertificate.Export(X509ContentType.Pfx, DevCertificatePassword);
            File.WriteAllBytes(certificatePath, pfxBytes);
            Console.WriteLine($"[Gateway][TLS] Development certificate created: {certificatePath}");
        }

        var rawPfx = File.ReadAllBytes(certificatePath);

        return new X509Certificate2(
            rawPfx,
            DevCertificatePassword,
            X509KeyStorageFlags.UserKeySet |
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateDevelopmentCertificate()
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        using var temporaryCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2));

        // Export/import once here to detach from the RSA object lifetime.
        return new X509Certificate2(
            temporaryCertificate.Export(X509ContentType.Pfx, DevCertificatePassword),
            DevCertificatePassword,
            X509KeyStorageFlags.UserKeySet |
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.Exportable);
    }
}