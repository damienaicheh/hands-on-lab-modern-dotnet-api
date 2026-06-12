import "@typespec/http";

using Http;

namespace DocumentAPI;

model ProblemDetails {
  type?: string;
  title?: string;
  status?: int32;
  detail?: string;
  instance?: string;
}

model BadRequestProblemResponse {
  ...BadRequestResponse;
  @header contentType: "application/problem+json";
  ...Body<ProblemDetails>;
}

model NotFoundProblemResponse {
  ...NotFoundResponse;
  @header contentType: "application/problem+json";
  ...Body<ProblemDetails>;
}

model ConflictProblemResponse {
  @statusCode statusCode: 409;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}

model PayloadTooLargeProblemResponse {
  @statusCode statusCode: 413;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}

model UnauthorizedProblemResponse {
  ...UnauthorizedResponse;
  @header contentType: "application/problem+json";
  ...Body<ProblemDetails>;
}

model InternalServerErrorProblemResponse {
  @statusCode statusCode: 500;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}

model BadGatewayProblemResponse {
  @statusCode statusCode: 502;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}

model ServiceUnavailableProblemResponse {
  @statusCode statusCode: 503;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}
