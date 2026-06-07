import "@typespec/http";
import "./error.tsp";

using Http;

namespace DocumentAPI;

model DocumentMetadata {
  title?: string;
  description?: string;
  tags?: string[];
  source?: string;
}

alias SupportedDocumentContentType =
  | "application/pdf"
  | "text/plain"
  | "application/msword"
  | "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

model Document {
  id: string;
  fileName?: string;
  contentType?: string;
  size?: int64;
  metadata?: DocumentMetadata;
}

model DocumentListResponse {
  ...OkResponse;
  ...Body<Document[]>;
}

model DocumentCreatedResponse {
  ...CreatedResponse;
  ...Body<Document>;
}

model DocumentDownloadResponse {
  ...OkResponse;
  ...Body<File<SupportedDocumentContentType>>;
}
