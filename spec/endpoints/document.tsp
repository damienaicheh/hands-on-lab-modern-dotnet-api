import "./../models/error.tsp";
import "./../models/documents.tsp";

using Http;

namespace DocumentAPI;

@route("/documents")
@tag("Documents")
@useAuth(BearerAuth)
interface Documents {
  @doc("Search documents using free text and metadata filters.")
  @route("/search")
  @get search(
    @query query?: string,
    @query title?: string,
    @query tag?: string,
    @query contentType?: string,
  ): DocumentListResponse | DocumentErrorResponse | DocumentUnauthorizedResponse;

  @doc("Upload a PDF, TXT, DOC, or DOCX document with metadata.")
  @post upload(
    @header contentType: "multipart/form-data",
    @multipartBody body: {
      file: HttpPart<File<SupportedDocumentContentType>>;
      metadata: HttpPart<DocumentMetadata>;
    },
  ): DocumentCreatedResponse | DocumentErrorResponse | DocumentUnauthorizedResponse;

  @doc("Download the file content of a document.")
  @route("/{id}/content")
  @get download(@path id: string): DocumentDownloadResponse | DocumentNotFoundResponse | DocumentUnauthorizedResponse;
}
