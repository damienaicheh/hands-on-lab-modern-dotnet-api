namespace DocumentAPI.Extensions;

using System.Security.Cryptography;

/// <summary>
/// Provides byte-array extension methods for document hash operations.
/// </summary>
internal static class FileHashExtensions
{
    /// <summary>
    /// Computes the MD5 content hash used to detect duplicate documents.
    /// </summary>
    internal static string ComputeContentHash(this byte[] content)
    {
        // MD5 is used to align the duplicate-detection hash with the Content-MD5 integrity contract, not for security.
#pragma warning disable CA5351
        return Convert.ToHexString(content.ComputeMd5());
#pragma warning restore CA5351
    }

    /// <summary>
    /// Computes the MD5 hash of the document content used to verify storage integrity.
    /// </summary>
    internal static byte[] ComputeMd5(this byte[] content)
    {
        // MD5 is used solely to satisfy the Content-MD5 integrity contract of Azure Blob Storage, not for security.
#pragma warning disable CA5351
        return MD5.HashData(content);
#pragma warning restore CA5351
    }
}