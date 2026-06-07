import "@typespec/http";

using Http;

namespace DocumentAPI;

enum HealthStatus {
  Healthy,
  Degraded,
  Unhealthy,
}

model HealthyOrDegradedStatus {
  status: HealthStatus.Healthy | HealthStatus.Degraded;
}

model UnhealthyStatus {
  status: HealthStatus.Unhealthy;
}

model HealthCheckOkResponse {
  @statusCode statusCode: 200;
  @body body: HealthyOrDegradedStatus;
}

model HealthCheckUnavailableResponse {
  @statusCode statusCode: 503;
  @body body: UnhealthyStatus;
}