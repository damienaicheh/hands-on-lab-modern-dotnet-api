import "@typespec/http";

using Http;

namespace DocumentAPI;

@doc("The API version query parameter.")
model ApiVersionParameter {
  @query("api-version")
  @minLength(1)
  @doc("The API version to use for this operation.")
  apiVersion: string;
}