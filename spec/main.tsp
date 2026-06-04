import "@typespec/http";
import "./models/error.tsp";
import "./endpoints/document.tsp";
import "./endpoints/health.tsp";

@service(#{ title: "Document Service" })
namespace DocumentAPI;
