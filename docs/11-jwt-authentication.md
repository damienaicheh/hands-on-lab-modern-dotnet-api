# Lab 11 - JWT Authentication

The document API now exposes useful operations. In this lab, you will protect those operations with JWT bearer authentication while keeping `/health` anonymous.

Authentication is configured from the options already provided in the starter.

You will protect the document workflow, not the whole application. Operational endpoints such as `/health` remain open so monitoring can keep working.

## What You Will Learn

In this lab, you will:

- Register JWT bearer authentication.
- Validate issuer, audience, signing key, and lifetime.
- Return a predictable `401 Unauthorized` response.
- Protect `/documents` endpoints.
- Keep Swagger usable with bearer tokens.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Authentication options, appsettings, and test token helpers are already provided.

## Register JWT Bearer Authentication

Open `Program.cs` and add JWT bearer authentication:

JWT bearer authentication lets the API validate a signed token without calling an external service for every request. The issuer, audience, and signing key define which tokens this API trusts.

Search for "TODO Lab 11: Register JWT bearer authentication and configure token validation" to find the right place to add this code:

```csharp
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = documentApiOptions.Authentication.RequireHttpsMetadata;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = documentApiOptions.Authentication.Issuer,
			ValidateAudience = true,
			ValidAudience = documentApiOptions.Authentication.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(documentApiOptions.Authentication.SigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1),
		};
	});
```

## Return A Clean 401 Response

Inside `AddJwtBearer`, configure `OnChallenge`:

The default challenge response can vary depending on middleware behavior. Returning `ProblemDetails` gives clients a predictable JSON shape.

```csharp
options.Events = new JwtBearerEvents
{
	OnChallenge = async context =>
	{
		context.HandleResponse();
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		context.Response.ContentType = "application/problem+json";
		await context.Response.WriteAsJsonAsync(new ProblemDetails
		{
			Status = StatusCodes.Status401Unauthorized,
			Title = "Unauthorized",
			Detail = "Access is unauthorized.",
		});
	},
};
```

## Enable Authentication Middleware

Then enable the middleware before endpoint execution (`app.MapHealthEndpoints();`):

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

## Protect Document Endpoints

Open `DocumentEndpoints.cs` and require authorization on the documents group:

Authorization is applied at the route group level so every current and future `/documents` endpoint inherits the same protection by default.

```csharp
var v1Group = documentGroup.MapGroup("/documents")
	.WithTags("Documents")
	.RequireAuthorization()
	.HasApiVersion(new ApiVersion(1));
```

If you open `HealthEndpoints.cs` you will see that the health endpoints has the `.AllowAnonymous()` configuration which allows them to be called without authentication.

## Add Bearer Support To Swagger

Go back to `Program.cs` inside `AddSwaggerGen`, add a bearer security definition at beginning of the configuration:

```csharp
var bearerSecurityScheme = new OpenApiSecurityScheme
{
	Name = "Authorization",
	Type = SecuritySchemeType.Http,
	Scheme = "bearer",
	BearerFormat = "JWT",
	In = ParameterLocation.Header,
	Description = "Provide a valid JWT bearer token.",
};

options.AddSecurityDefinition("Bearer", bearerSecurityScheme);
```

This does not authenticate anyone by itself. It only teaches Swagger UI how to send an `Authorization: Bearer ...` header when you test protected endpoints.

Then apply that scheme to the generated operations:

```csharp
options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
{
	[new OpenApiSecuritySchemeReference("Bearer", hostDocument: document, externalResource: null)] = [],
});
```

Without the requirement, Swagger UI knows what a bearer token is, but the operations are not annotated as requiring it.

## Run And Test Authentication

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http`. First send the `Health` request to confirm the API is running.

Then call the search endpoint without a token:

```txt
/documents/search?api-version=1.0
```

It should return `401 Unauthorized`.

To test the authenticated path, generate a valid JWT :


1. Open https://jwt.io/
2. In the Decoded section, update the Header.
3. Use this Header:
```json
{
	"alg": "HS256",
	"typ": "JWT"
}
```

4. Use this Payload template:

```json
{
	"iss": "DocumentAPI",
	"aud": "DocumentAPIClient",
	"sub": "lab-user",
	"name": "DocumentAPI User",
	"iat": 1735689600,
	"nbf": 1735689600,
	"exp": 2145916800,
	"jti": "lab-long-lived-token-001"
}
```

    Notes:
    - iat/nbf = 1735689600 (2025-01-01T00:00:00Z)
    - exp = 2145916800 (2038-01-01T00:00:00Z)
    - This is intentionally long-lived for testing purposes.

5. In Verify Signature:
- Set the secret to: `document-api-signing-key-to-randomly-generate`
- Make sure Secret base64 encoded is disabled

6. Copy the token from the Encoded section.

Paste it into the `@token` variable near the top of `src/http/requests.http`:

```http
@token=PASTE_VALID_JWT_HERE
```

Then uncomment the `Authorization: Bearer {{token}}` for each document request and send it again. With a valid token, the request should pass authentication and continue to the normal document endpoint behavior.

Add back the `Authorization: Bearer {{token}}` header on the document request and send it again. With a valid token, the request should pass authentication and continue to the normal document endpoint behavior.

<div class="task" data-title="Validation">

> Confirm that `/documents` requests in `src/http/requests.http` require a token.
>
> Confirm that `/health` still works anonymously.

</div>

---