using System.Security.Cryptography.X509Certificates;

namespace Gateway.Security;

public sealed class TlsCertificateLoader
{
    public X509Certificate2 Load(string path, string? password = null)
    {
        return string.IsNullOrWhiteSpace(password)
            ? new X509Certificate2(path)
            : new X509Certificate2(path, password);
    }
}
