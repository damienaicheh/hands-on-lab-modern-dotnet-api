import "./../models/error.tsp";
import "./../models/documents.tsp";
import "./../models/common.tsp";

using Http;

namespace DocumentAPI;

@route("/documents")
@tag("Documents")
@useAuth(BearerAuth)
interface Documents {
  @doc("Search documents using free text and metadata filters.")
  @route("/search")
  @get search(
    ...ApiVersionParameter,
    @query query?: string,
    @query title?: string,
    @query tag?: string,
    @query contentType?: string,
  ): DocumentListResponse
    | BadRequestProblemResponse
    | InternalServerErrorProblemResponse
    | ServiceUnavailableProblemResponse
    | UnauthorizedProblemResponse;

  @doc("Upload a PDF, TXT, DOC, or DOCX document with metadata.")
  @post upload(
    ...ApiVersionParameter,
    @header contentType: "multipart/form-data",
    @multipartBody body: {
      file: HttpPart<File<SupportedDocumentContentType>>;
      metadata: HttpPart<DocumentMetadata>;
    },
  ): DocumentCreatedResponse
    | BadRequestProblemResponse
    | ConflictProblemResponse
    | InternalServerErrorProblemResponse
    | BadGatewayProblemResponse
    | ServiceUnavailableProblemResponse
    | PayloadTooLargeProblemResponse
    | UnauthorizedProblemResponse;

  @doc("Download the file content of a document.")
  @route("/{id}/content")
  @get download(
    ...ApiVersionParameter,
    @path id: string,
  ): DocumentDownloadResponse
    | BadRequestProblemResponse
    | NotFoundProblemResponse
    | InternalServerErrorProblemResponse
    | ServiceUnavailableProblemResponse
    | UnauthorizedProblemResponse;
}
