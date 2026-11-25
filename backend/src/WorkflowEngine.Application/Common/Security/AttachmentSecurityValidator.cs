using System.Text;
using WorkflowEngine.Application.Common.Exceptions;

namespace WorkflowEngine.Application.Common.Security;

public record ValidatedAttachmentMetadata(
    string SanitizedFileName,
    string CanonicalContentType,
    string Extension
);

/// <summary>
/// Server-side binary signature (magic bytes), extension allowlist, MIME consistency,
/// full-payload EICAR malware screening, and header-safe filename sanitizer.
/// </summary>
public static class AttachmentSecurityValidator
{
    public const long MaxAttachmentBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly byte[] EicarSignatureBytes = Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipOpenXmlMagic = [0x50, 0x4B, 0x03, 0x04];

    public static ValidatedAttachmentMetadata ValidateAndInspect(
        string? fileName,
        string? declaredContentType,
        byte[]? content)
    {
        if (content == null || content.Length == 0)
        {
            throw new AttachmentValidationException(
                AttachmentValidationException.InvalidFileTypeCode,
                "INVALID_FILE_TYPE: Attachment content cannot be empty.");
        }

        if (content.Length > MaxAttachmentBytes)
        {
            throw new AttachmentValidationException(
                AttachmentValidationException.FileTooLargeCode,
                $"Attachment exceeds maximum allowed size of {MaxAttachmentBytes / (1024 * 1024)} MB.");
        }

        // 1. Full-buffer EICAR malware scan across the entire payload regardless of file size
        if (ContainsEicarSignature(content))
        {
            throw new AttachmentValidationException(
                AttachmentValidationException.MalwareDetectedCode,
                "MALWARE_DETECTED: Malware signature detected during attachment security scan.");
        }

        // 2. Sanitize filename & enforce strict extension allowlist
        string sanitizedName = SanitizeDownloadFileName(fileName);
        string ext = Path.GetExtension(sanitizedName).ToLowerInvariant();
        string normalizedMime = NormalizeContentType(declaredContentType);

        return ext switch
        {
            ".pdf" => ValidateFormat(
                sanitizedName,
                ext,
                content,
                PdfMagic,
                normalizedMime,
                canonicalMime: "application/pdf",
                allowedMimes: ["application/pdf", "application/octet-stream"]),

            ".png" => ValidateFormat(
                sanitizedName,
                ext,
                content,
                PngMagic,
                normalizedMime,
                canonicalMime: "image/png",
                allowedMimes: ["image/png", "application/octet-stream"]),

            ".jpg" or ".jpeg" => ValidateFormat(
                sanitizedName,
                ext,
                content,
                JpegMagic,
                normalizedMime,
                canonicalMime: "image/jpeg",
                allowedMimes: ["image/jpeg", "image/jpg", "application/octet-stream"]),

            ".docx" => ValidateFormat(
                sanitizedName,
                ext,
                content,
                ZipOpenXmlMagic,
                normalizedMime,
                canonicalMime: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                allowedMimes:
                [
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/octet-stream",
                    "application/zip"
                ]),

            ".xlsx" => ValidateFormat(
                sanitizedName,
                ext,
                content,
                ZipOpenXmlMagic,
                normalizedMime,
                canonicalMime: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                allowedMimes:
                [
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "application/octet-stream",
                    "application/zip"
                ]),

            _ => throw new AttachmentValidationException(
                AttachmentValidationException.InvalidFileTypeCode,
                $"INVALID_FILE_TYPE: File extension '{(string.IsNullOrEmpty(ext) ? "<none>" : ext)}' is not permitted. Allowed formats: PDF, PNG, JPEG, DOCX, XLSX.")
        };
    }

    public static bool ContainsEicarSignature(ReadOnlySpan<byte> content)
    {
        return content.Length >= EicarSignatureBytes.Length &&
               content.IndexOf(EicarSignatureBytes) >= 0;
    }

    /// <summary>
    /// Strips path segments, CRLF characters, quotes, and control bytes to prevent HTTP header injection.
    /// </summary>
    public static string SanitizeDownloadFileName(string? rawFileName)
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return "attachment.bin";
        }

        // Normalize both Windows and Unix path separators before extracting leaf name
        string normalizedSeparators = rawFileName.Replace('\\', '/');
        int lastSlash = normalizedSeparators.LastIndexOf('/');
        string leaf = lastSlash >= 0 ? normalizedSeparators[(lastSlash + 1)..] : normalizedSeparators;

        var sb = new StringBuilder(leaf.Length);
        foreach (char c in leaf)
        {
            if (c < 32 || c == 127 || c is '\r' or '\n' or '"' or '\'' or '\\' or '/' or ';' or ':' or '*' or '?' or '<' or '>' or '|')
            {
                continue;
            }

            sb.Append(c);
        }

        string cleaned = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned is "." or "..")
        {
            return "attachment.bin";
        }

        return cleaned.Length > 200 ? cleaned[^200..] : cleaned;
    }

    private static ValidatedAttachmentMetadata ValidateFormat(
        string sanitizedName,
        string ext,
        ReadOnlySpan<byte> content,
        ReadOnlySpan<byte> expectedMagic,
        string normalizedMime,
        string canonicalMime,
        string[] allowedMimes)
    {
        if (!string.IsNullOrEmpty(normalizedMime) &&
            !allowedMimes.Any(m => string.Equals(m, normalizedMime, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AttachmentValidationException(
                AttachmentValidationException.InvalidFileTypeCode,
                $"INVALID_FILE_TYPE: Declared Content-Type '{normalizedMime}' does not match extension '{ext}'.");
        }

        if (content.Length < expectedMagic.Length ||
            !content[..expectedMagic.Length].SequenceEqual(expectedMagic))
        {
            throw new AttachmentValidationException(
                AttachmentValidationException.InvalidFileTypeCode,
                $"INVALID_FILE_TYPE: Binary file signature (magic bytes) does not match '{ext}'.");
        }

        return new ValidatedAttachmentMetadata(sanitizedName, canonicalMime, ext);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        int semiIdx = contentType.IndexOf(';');
        string mediaType = semiIdx >= 0 ? contentType[..semiIdx] : contentType;
        return mediaType.Trim().ToLowerInvariant();
    }
}
