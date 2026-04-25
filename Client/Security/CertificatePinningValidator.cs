using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Client.Security;

public static class CertificatePinningValidator
{
    public static bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // Demo baseline: accept certificate.
        // TODO: Pin Gateway certificate fingerprint for Internet demo.
        return true;
    }
}
