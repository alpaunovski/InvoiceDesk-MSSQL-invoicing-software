using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using InvoiceDesk.Data;
using InvoiceDesk.Helpers;
using InvoiceDesk.Models;
using iText.Kernel.Pdf;
using iText.Signatures;
using iText.Commons.Bouncycastle.Cert;
using iText.Bouncycastle.X509;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace InvoiceDesk.Services;

/// <summary>
/// Applies KEP/QES signatures to issued invoice PDFs using certificates from the Windows store (smart cards/tokens).
/// </summary>
public class PdfSigningService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly PdfExportService _pdfExportService;
    private readonly ILogger<PdfSigningService> _logger;

    public PdfSigningService(IDbContextFactory<AppDbContext> dbFactory, PdfExportService pdfExportService, ILogger<PdfSigningService> logger)
    {
        _dbFactory = dbFactory;
        _pdfExportService = pdfExportService;
        _logger = logger;
    }

    /// <summary>
    /// Signs the issued PDF for an invoice and persists the signed bytes and metadata.
    /// </summary>
    public async Task<string> SignIssuedPdfAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices.Include(i => i.Company).FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.IssuedAtUtc == null)
        {
            throw new InvalidOperationException("Only issued invoices can be signed");
        }

        _logger.LogInformation("KEP/QES signing requested for invoice {InvoiceId}", invoiceId);
        var unsignedPath = await _pdfExportService.ExportPdfAsync(invoiceId, null, false, cancellationToken);
        var outputDir = Path.Combine(Path.GetDirectoryName(unsignedPath) ?? string.Empty, "signed");
        Directory.CreateDirectory(outputDir);
        var fileName = invoice.SignedPdfFileName ?? BuildSignedFileName(unsignedPath);
        var signedPath = Path.Combine(outputDir, fileName);

        var certificate = SelectCertificate();
        if (certificate == null)
        {
            throw new OperationCanceledException("Certificate selection was cancelled");
        }

        await Task.Run(() => SignPdf(unsignedPath, signedPath, certificate, invoice), cancellationToken);

        var bytes = await File.ReadAllBytesAsync(signedPath, cancellationToken);
        invoice.SignedPdf = bytes;
        invoice.SignedPdfFileName = Path.GetFileName(signedPath);
        invoice.SignedPdfSha256 = HashHelper.ComputeSha256(bytes);
        invoice.SignedPdfCreatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("KEP/QES signing complete for invoice {InvoiceId}; saved to {Path}", invoiceId, signedPath);
        return signedPath;
    }

    private static string BuildSignedFileName(string unsignedPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(unsignedPath);
        return string.IsNullOrWhiteSpace(baseName)
            ? "signed-invoice.pdf"
            : $"{baseName}-signed.pdf";
    }

    private static X509Certificate2? SelectCertificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        // Filter for certificates capable of digital signatures; smart card/KSP backed keys are supported.
        var candidates = new X509Certificate2Collection();
        foreach (var cert in store.Certificates)
        {
            if (!cert.HasPrivateKey)
            {
                continue;
            }

            var keyUsage = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            var hasDigitalSignature = keyUsage == null || (keyUsage.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation)) != 0;
            if (!hasDigitalSignature)
            {
                continue;
            }

            candidates.Add(cert);
        }

        var selection = X509Certificate2UI.SelectFromCollection(
            candidates,
            "Select certificate",
            "Choose a KEP/QES certificate from your smart card/token to sign the invoice PDF.",
            X509SelectionFlag.SingleSelection);

        return selection.Count > 0 ? selection[0] : null;
    }

    private void SignPdf(string sourcePath, string targetPath, X509Certificate2 certificate, Invoice invoice)
    {
        _logger.LogInformation("Signing PDF {Source} to {Target} with {Subject}", sourcePath, targetPath, certificate.Subject);
        using var reader = new PdfReader(sourcePath);
        using var output = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite);
        var signer = new PdfSigner(reader, output, new StampingProperties().UseAppendMode());

        signer.SetFieldName($"Sig{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        signer.SetCertificationLevel(PdfSigner.CERTIFIED_NO_CHANGES_ALLOWED);

        var externalSignature = new X509Certificate2SignatureAdapter(certificate);
        var bcCert = DotNetUtilities.FromX509Certificate(certificate);
        var chain = new IX509Certificate[] { new X509CertificateBC(bcCert) };
        signer.SignDetached(externalSignature, chain, null, null, null, 0, PdfSigner.CryptoStandard.CADES);
    }
}

/// <summary>
/// Minimal external signature adapter using an X509Certificate2 private key (RSA or ECDSA).
/// </summary>
internal sealed class X509Certificate2SignatureAdapter : IExternalSignature
{
    private readonly X509Certificate2 _certificate;
    private readonly string _hashAlgorithm;
    private readonly string _encryptionAlgorithm;

    public X509Certificate2SignatureAdapter(X509Certificate2 certificate, string hashAlgorithm = DigestAlgorithms.SHA256)
    {
        _certificate = certificate;
        _hashAlgorithm = hashAlgorithm;

        _encryptionAlgorithm = certificate.GetKeyAlgorithm()?.Contains("1.2.840.10045", StringComparison.Ordinal) == true
            ? "ECDSA"
            : "RSA";
    }

    public string GetHashAlgorithm() => _hashAlgorithm;

    public string GetEncryptionAlgorithm() => _encryptionAlgorithm;

    public string GetDigestAlgorithmName() => _hashAlgorithm;

    public string GetSignatureAlgorithmName() => _encryptionAlgorithm;

    public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

    public byte[] Sign(byte[] message)
    {
        if (_encryptionAlgorithm == "RSA")
        {
            using var rsa = _certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate does not have an RSA private key");
            return rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        using var ecdsa = _certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("Certificate does not have an ECDSA private key");
        return ecdsa.SignData(message, HashAlgorithmName.SHA256);
    }
}
