namespace DocumentAPI.Services.Documents;

/// <summary>
/// Represents the search criteria that can be applied to document queries.
/// </summary>
/// <param name="Query">The free-text query applied to searchable metadata fields.</param>
/// <param name="Title">The exact-match title filter.</param>
/// <param name="Tag">The exact-match tag filter.</param>
/// <param name="ContentType">The exact-match content type filter.</param>
public sealed record DocumentSearchCriteria(string? Query, string? Title, string? Tag, string? ContentType);
