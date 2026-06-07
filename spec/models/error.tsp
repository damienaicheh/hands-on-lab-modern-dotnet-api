import "@typespec/http";

using Http;

namespace DocumentAPI;

@error
model Error {
  code: int32;
  message: string;
}

@error
model UnauthorizedError {
  code: "UNAUTHORIZED";
  message: string;
}

model BadRequestErrorResponse {
  ...BadRequestResponse;
  ...Body<Error>;
}

model NotFoundErrorResponse {
  ...NotFoundResponse;
  ...Body<Error>;
}

model ConflictErrorResponse {
  @statusCode statusCode: 409;
  @body body: Error;
}

model PayloadTooLargeErrorResponse {
  @statusCode statusCode: 413;
  @body body: Error;
}

model UnauthorizedErrorResponse {
  ...UnauthorizedResponse;
  ...Body<UnauthorizedError>;
}

model InternalServerErrorApiResponse {
  @statusCode statusCode: 500;
  @body body: Error;
}

model BadGatewayApiErrorResponse {
  @statusCode statusCode: 502;
  @body body: Error;
}

model ServiceUnavailableApiErrorResponse {
  @statusCode statusCode: 503;
  @body body: Error;
}
