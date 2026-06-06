# Create a modern Dotnet API

## Summary of the labs

- Install the environment and dependencies
- Present and instanciate Typespec
- Create an API definition with Typespec and generate the Open API spec
- Use TypeSpec to generate a Dotnet API
- Create the Controllers in the API Project
- Use Kiota to generate a client for the API
- Use the generated client in a console application

- Create a new API project
- Dependency injection
- Implement the 4 endpoints:
   - Upload a document
       - Store document to the Storage Account
       - Store metadata to the SQL Database
       - Secrets management
   - Download a document
       - Retrieve document from the Storage Account
   - Search for documents based on metadata
       - Add Caching to the Search endpoint
   - Health check endpoint
- Manage DTOs
- Versioning of the API
- Add JWT authentication to the API
- Add unit tests for the API
- Add monitoring, observability and logging to the API
- Infuse GitHub Copilot tips like documentation generation, code generation, and testing


Diagram de sequence des appels d'API

## TODO

Vérifier retry pattern pour l'upload du document dans le Storage Account
Vérifier les champs de la BDD s'il n'y a pas des données inutiles
Vérifier les cas d'erreurs
Vérifier si les commentaires sont tous bons
Vérifier s'il n'y a pas de code mort