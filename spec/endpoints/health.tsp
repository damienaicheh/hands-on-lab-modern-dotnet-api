import "@typespec/http";
import "./../models/error.tsp";
import "./../models/health.tsp";
import "./../models/common.tsp";

using Http;

namespace DocumentAPI;

@route("/health")
interface Health {
  @doc("Returns the service health status.")
  @get check(...ApiVersionParameter): HealthCheckOkResponse | HealthCheckUnavailableResponse | BadRequestErrorResponse;
}
