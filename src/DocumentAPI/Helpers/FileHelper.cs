using System.Security.Cryptography;

namespace DocumentAPI.Helpers;

internal static class FileHelper
{
    /// <summary>
    /// Computes the MD5 content hash used to detect duplicate documents.
    /// </summary>
    internal static string ComputeContentHash(byte[] content)
    {
        // MD5 is used to align the duplicate-detection hash with the Content-MD5 integrity contract, not for security.
#pragma warning disable CA5351
        return Convert.ToHexString(ComputeMd5(content));
#pragma warning restore CA5351
    }


    /// <summary>
    /// Computes the MD5 hash of the document content used to verify storage integrity.
    /// </summary>
    internal static byte[] ComputeMd5(byte[] content)
    {
        // MD5 is used solely to satisfy the Content-MD5 integrity contract of Azure Blob Storage, not for security.
#pragma warning disable CA5351
        return MD5.HashData(content);
#pragma warning restore CA5351
    }
}