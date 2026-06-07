import "@typespec/http";
import "./../models/error.tsp";
import "./../models/health.tsp";

using Http;

namespace DocumentAPI;

@route("/health")
@tag("Health")
interface Health {
  @doc("Returns the service health status.")
  @get check(): HealthCheckOkResponse | HealthCheckUnavailableResponse | BadRequestErrorResponse;
}
