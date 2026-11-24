using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Identity.Api.Protocol;

public static class SigningKeys
{
    public const string SigningFile = "signing.pfx";
    public const string EncryptionFile = "encryption.pfx";

    public static void Configure(OpenIddictServerBuilder server, KeySettings keys, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction() && keys.Mode != KeyMode.Certificates)
        {
            throw new InvalidOperationException("Identity:Keys:Mode must be Certificates in Production.");
        }

        switch (keys.Mode)
        {
            case KeyMode.Ephemeral:
                server.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
                break;
            case KeyMode.Directory:
                var path = Require(keys.Path, "Identity:Keys:Path");
                var password = Require(keys.Password, "Identity:Keys:Password");
                Directory.CreateDirectory(path);
                server.AddSigningCertificate(LoadOrCreate(System.IO.Path.Combine(path, SigningFile), password, "identity-signing", X509KeyUsageFlags.DigitalSignature));
                server.AddEncryptionCertificate(LoadOrCreate(System.IO.Path.Combine(path, EncryptionFile), password, "identity-encryption", X509KeyUsageFlags.KeyEncipherment));
                break;
            case KeyMode.Certificates:
                var secret = Require(keys.Password, "Identity:Keys:Password");
                server.AddSigningCertificate(Load(Require(keys.SigningCertificatePath, "Identity:Keys:SigningCertificatePath"), secret));
                server.AddEncryptionCertificate(Load(Require(keys.EncryptionCertificatePath, "Identity:Keys:EncryptionCertificatePath"), secret));
                break;
            default:
                throw new InvalidOperationException("Identity:Keys:Mode must be Ephemeral, Directory or Certificates.");
        }
    }

    private static X509Certificate2 LoadOrCreate(string file, string password, string subject, X509KeyUsageFlags usage)
    {
        if (!File.Exists(file))
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(2));
            var temporary = file + "." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporary, certificate.Export(X509ContentType.Pfx, password));
            try
            {
                File.Move(temporary, file, overwrite: false);
            }
            catch (IOException) when (File.Exists(file))
            {
                File.Delete(temporary);
            }
        }

        return Load(file, password);
    }

    private static X509Certificate2 Load(string file, string password)
    {
        if (!File.Exists(file))
        {
            throw new InvalidOperationException($"Certificate file '{file}' does not exist.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(file, password, X509KeyStorageFlags.EphemeralKeySet);
    }

    private static string Require(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} is required.") : value;
}
