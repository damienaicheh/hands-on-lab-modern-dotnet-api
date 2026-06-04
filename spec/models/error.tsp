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
