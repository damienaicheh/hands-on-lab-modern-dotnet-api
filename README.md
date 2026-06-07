# DocumentAPI - .NET 10 best-practices review

## Scope reviewed
- `/tmp/workspace/damienaicheh/hands-on-lab-modern-dotnet-api/src/DocumentAPI`
- `/tmp/workspace/damienaicheh/hands-on-lab-modern-dotnet-api/test/DocumentAPI.Tests`

## Missing / bad practices (based on Microsoft guidance)

| # | Area | Current code evidence | Why this is a problem | Microsoft guidance | Recommended improvement |
|---|---|---|---|---|---|
| 1 | JWT security | `Program.cs` sets `options.RequireHttpsMetadata = false` unconditionally | Token metadata can be fetched over insecure channels when misconfigured | [Configure JWT bearer authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication) | Set `RequireHttpsMetadata = true` by default, and only disable it in controlled local development scenarios. |
| 2 | Options/config validation | `DocumentApiOptions` is bound, but there is no `ValidateOnStart()` or data-annotation/custom validation for required settings | Missing/invalid settings fail late at runtime instead of startup | [Options pattern in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options) | Add strongly-typed options validation (`Validate`, `ValidateDataAnnotations`, `ValidateOnStart`) for auth/storage/database settings. |
| 3 | API error contract | Endpoints return custom `ApiError` instead of RFC 7807 `ProblemDetails` | Inconsistent error interoperability and weaker tooling integration | [Handle errors in ASP.NET Core web APIs](https://learn.microsoft.com/aspnet/core/web-api/handle-errors) and [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines/blob/vNext/Guidelines.md#7102-error-condition-responses) | Use `ProblemDetails`/`ValidationProblemDetails` (or `IProblemDetailsService`) for standard API error responses. |
| 4 | File upload memory usage | `DocumentEndpoints.UploadAsync` reads uploaded files fully into memory (`MemoryStream` + `ToArray`) | Large uploads increase memory pressure and denial-of-service risk | [Upload files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads) | Stream upload directly to storage and hash incrementally instead of buffering whole payload in RAM. |
| 5 | Swagger exposure in production | Swagger middleware is enabled unconditionally (`app.UseSwagger(); app.UseSwaggerUI();`) | Public API metadata/UI may be exposed unintentionally in production | [Get started with Swashbuckle and ASP.NET Core](https://learn.microsoft.com/aspnet/core/tutorials/getting-started-with-swashbuckle) | Restrict Swagger to development/internal environments or protect it with authentication/authorization. |
| 6 | API versioning strategy | Version validation is manual query-string checking (`ApiVersionValidation`) | Reinvents platform features and can drift from standard API-versioning behavior | [API versioning in ASP.NET Core](https://learn.microsoft.com/aspnet/core/web-api/advanced/advanced-formatting#api-versioning) | Use the official ASP.NET Core API versioning approach/policies instead of custom validation logic. |

## Notes
- Existing test baseline currently has failing integration tests unrelated to this documentation change (`dotnet test` reports multiple `InternalServerError` responses where tests expect specific status codes).
