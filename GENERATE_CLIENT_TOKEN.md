# Generate a JWT Client Token with jwt.io

This guide explains how to generate a valid JWT token to call protected DocumentAPI endpoints.

## 1. API Expected Values

The API validates the following:
- Issuer
- Audience
- Signature
- Expiration

Current configuration:
- Issuer: DocumentAPI
- Audience: DocumentAPIClient
- Signing key: document-api-dev-signing-key-change-me
- Expected algorithm: HS256
- Clock skew tolerance: 1 minute

## 2. Steps in jwt.io

1. Open https://jwt.io/
2. In the Decoded section, update the Header.
3. Use this Header:

    {
      "alg": "HS256",
      "typ": "JWT"
    }

4. Use this Payload template:

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

    Notes:
    - iat/nbf = 1735689600 (2025-01-01T00:00:00Z)
    - exp = 2145916800 (2038-01-01T00:00:00Z)
    - This is intentionally long-lived for testing purposes.

5. In Verify Signature:
- Set the secret to: document-api-dev-signing-key-change-me
- Make sure Secret base64 encoded is disabled

6. Copy the token from the Encoded section.

## 3. Where to Put the Token in the Project

Paste the token into the token variable in:
- src/http/requests.http

Example:
- @token=PASTE_VALID_JWT_HERE

## 4. Understand the Token Payload

Think of a token as a signed identity card:

- Header: says how the card is signed.
- Payload: contains identity and validity information (claims).
- Signature: proves the token was created with the shared secret.

Important:
- The payload is not encrypted by default. It is only encoded.
- Do not put secrets in payload fields.

### Where payload validation values come from in this project

The values you put in the payload are validated against configuration loaded by the API at startup.

Source of truth:
- Authentication config is defined in src/DocumentAPI/appsettings.json under DocumentApi:Authentication.
- The API reads that section in src/DocumentAPI/Program.cs.
- JWT validation also happens in src/DocumentAPI/Program.cs through TokenValidationParameters.

Concretely, these mappings are used (see appsettings.json):
- payload.iss must match DocumentApi:Authentication:Issuer
- payload.aud must match DocumentApi:Authentication:Audience
- token signature must be generated with DocumentApi:Authentication:SigningKey

If you change Issuer, Audience, or SigningKey in configuration, you must regenerate tokens with the new values.

### How DocumentAPI validates a token

At a high level, the API does this when you call a protected endpoint:

1. Read and decode the token.
2. Verify the signature with the configured secret.
3. Check iss matches DocumentAPI.
4. Check aud matches DocumentAPIClient.
5. Check token time validity (nbf/exp, with 1 minute clock skew).
6. If one check fails, return 401 Unauthorized.

### Header fields

- alg: Signing algorithm. Must be HS256 in this project.
- typ: Token type. Standard value is JWT.

### Payload claims explained

- iss (issuer): Who created the token.
  - In this project, it must be DocumentAPI.
  - If wrong: 401 Unauthorized.

- aud (audience): Which API the token is for.
  - In this project, it must be DocumentAPIClient.
  - If wrong: 401 Unauthorized.

- sub (subject): Technical user/client identifier.
  - Example: lab-user.
  - Useful for identifying the caller in logs.

- name: Human-readable display name.
  - Example: DocumentAPI User.
  - Useful for diagnostics and tracing.

- iat (issued at): When the token was created (Unix timestamp, seconds).
  - Mainly informational in this API.

- nbf (not before): Token is invalid before this timestamp.
  - If set in the future: 401 until that time.

- exp (expiration): Token is invalid after this timestamp.
  - If expired: 401 Unauthorized.

- jti (JWT ID): Unique token identifier.
  - Useful for traceability and audit scenarios.

## 5. Important Rules for iat, nbf, exp

- iat, nbf, and exp must be Unix timestamps in seconds.
- exp must be in the future.
- The API allows a 1-minute clock skew, but do not rely on it for long tests.

### About "infinite" tokens in labs

For quick testing, teams often call a very long-lived token an "infinite" token.
In reality, it is not infinite: it still has an exp value, just far in the future.

This guide uses exp = 2145916800 (2038-01-01) to avoid frequent regeneration during workshops.

Important: this approach is for training/lab convenience only.
In production, always use short-lived tokens and proper token issuance/rotation.
