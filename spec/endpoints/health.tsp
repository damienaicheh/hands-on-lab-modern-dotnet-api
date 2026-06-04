import "@typespec/http";
import "./../models/health.tsp";

using Http;

namespace DocumentAPI;

@route("/health")
interface Health {
  @doc("Returns the service health status.")
  @get check(): HealthCheckOkResponse | HealthCheckUnavailableResponse;
}
