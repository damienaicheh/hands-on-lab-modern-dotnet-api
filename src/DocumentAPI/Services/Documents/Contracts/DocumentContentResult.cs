namespace DocumentAPI.Services.Documents;

/// <summary>
/// Represents the binary content returned for a downloaded document.
/// </summary>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="Content">The binary content stream.</param>
public sealed record DocumentContentResult(string FileName, string ContentType, Stream Content);
