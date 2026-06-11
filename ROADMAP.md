# Create a modern Dotnet API

## Summary of the labs

- Install the environment and dependencies
- Present and instanciate Typespec
- Create an API definition with Typespec and generate the Open API spec
- Use TypeSpec to generate a Dotnet API
- Create the Controllers in the API Project
- Use Kiota to generate a client for the API
- Use the generated client in a console application

- Start from the skeleton of the API project
- Setup appsettings and configuration
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


**Lab 1 - Skeleton + Swagger**

- Explorer la structure du projet `DocumentAPI`.
- Vérifier que DTOs, options et appsettings sont déjà fournis.
- Ajouter les services Swagger/OpenAPI dans Program.cs.
- Exposer `/swagger` en environnement Development.
- Ajouter les premiers metadata Swagger sur les endpoints existants.
- Vérifier que `/swagger/v1/swagger.json` répond.

**Lab 2 - SQL Metadata Persistence**

- Créer l’entité `Document`.
- Ajouter `DocumentDbContext`.
- Configurer le mapping EF Core SQL Server.
- Ajouter la migration `InitialCreate`.
- Créer `AddDocumentServices`.
- Enregistrer `DocumentDbContext` dans la DI.
- Appliquer les migrations au démarrage avec `InitializeDocumentDatabaseAsync`.

**Lab 3 - Blob Storage**

- Créer `IDocumentStorageService`.
- Implémenter `AzureBlobDocumentStorageService`.
- Utiliser `DefaultAzureCredential`.
- Configurer `BlobServiceClient` avec l’URI du Storage Account.
- Créer ou récupérer le container cible.
- Ajouter les méthodes `SaveAsync`, `OpenReadAsync`, `DeleteAsync`.
- Enregistrer le service de stockage dans `AddDocumentServices`.

**Lab 4 - Upload Happy Path**

- Utiliser les contrats métier d’upload fournis.
- Créer `DocumentService` et implémenter `IDocumentService`.
- Implémenter `DocumentService.UploadAsync` laisser les autres méthodes non implémentées pour l’instant.
- Lire le fichier multipart dans l’endpoint `POST /documents`.
- Lire et désérialiser la metadata.
- Sauvegarder le fichier dans Blob Storage.
- Sauvegarder les métadonnées dans SQL.
- Retourner `201 Created` avec un `DocumentDto`.

**Lab 5 - Upload Robustesse**

- Ajouter la validation du multipart.
- Vérifier présence du fichier et de la metadata.
- Valider taille maximale et content types autorisés.
- Calculer le hash du contenu.
- Détecter les documents doublons.
- Retourner `409 Conflict` sur doublon.
- Nettoyer le blob si l’écriture SQL échoue.
- Ajouter la résilience SQL avec Polly.
- Mapper les erreurs storage/database vers des réponses propres.

**Lab 6 - Download + Search**

- Implémenter `DownloadAsync` dans `IDocumentService`.
- Rechercher la metadata SQL par identifiant.
- Lire le contenu depuis Blob Storage.
- Implémenter `GET /documents/{id}/content`.
- Retourner `404 Not Found` si le document n’existe pas.
- Implémenter `SearchAsync`.
- Ajouter les filtres `query`, `title`, `tag`, `contentType`.
- Implémenter `GET /documents/search`.

**Lab 7 - Cache Sur Search**

- Ajouter `IMemoryCache`.
- Ajouter une option `Search.CacheTtlSeconds`.
- Créer une clé de cache déterministe à partir des critères.
- Mettre en cache les résultats de recherche.
- Ajouter une version de cache partagée.
- Invalider le cache après un upload réussi.
- Vérifier le comportement cache hit/cache miss.

**Lab 8 - Health Endpoint**

- Créer les modèles de réponse health.
- Créer `IHealthStatusService`.
- Vérifier la connectivité SQL.
- Vérifier la connectivité Blob Storage.
- Retourner `Healthy`, `Degraded` ou `Unhealthy`.
- Implémenter `GET /health`.
- Garder `/health` sans authentification.
- Ajouter les détails par dépendance en mode dégradé.

**Lab 9 - Tests Unitaires**

- Ajouter les dépendances de test nécessaires.
- Tester `DocumentService` avec EF InMemory.
- Créer un fake `IDocumentStorageService`.
- Tester upload happy path.
- Tester détection de doublon.
- Tester download document manquant.
- Tester download document existant.
- Tester search avec cache.
- Introduire Copilot pour générer les cas limites.

**Lab 10 - API Versioning**

- Configurer `Asp.Versioning`.
- Configurer la lecture de version par query string.
- Créer un groupe versionné pour `/documents`.
- Exiger `api-version=1.0` sur les endpoints documents.
- Garder `/health` hors versioning ou explicitement public.
- Générer Swagger par version.
- Vérifier les réponses quand `api-version` manque.

**Lab 11 - JWT Authentication**

- Ajouter l’authentification Bearer JWT.
- Lire issuer, audience et signing key depuis les options fournies.
- Configurer `TokenValidationParameters`.
- Ajouter `UseAuthentication` et `UseAuthorization`.
- Protéger les endpoints `/documents`.
- Garder `/health` anonyme.
- Retourner une réponse `401` propre.
- Ajouter des tests d’accès non authentifié et authentifié.

**Lab 12 - Observability**

Partie A - Correlation ID:

- Créer `CorrelationIdMiddleware`.
- Lire ou générer `X-Correlation-Id`.
- Ajouter le header à la réponse.
- Brancher le middleware dans Program.cs.
- Activer le HTTP logging utile.
- Tester l’écho du correlation id.

Partie B - Application Insights:

- Ajouter `AddApplicationInsightsTelemetry`.
- Configurer la connection string via options ou variable d’environnement.
- Créer un `TelemetryInitializer`.
- Enrichir la télémétrie avec service name, cloud role et correlation id.
- Créer `IDocumentActivityMonitor`.
- Émettre événements et métriques métier sur upload/search/download.
- Ajouter des logs structurés sur erreurs et opérations importantes.
- Tester l’émission de telemetry avec un channel fake.

## Version simplifiée pour l'atelier

Objectif: chaque lab doit demander le moins de manipulations possible. Les participants doivent modifier 1 à 3 fichiers maximum par lab, idéalement autour d'un seul point d'entrée métier. Les fichiers techniques transverses doivent être pré-créés dans le starter avec des `TODO` ciblés et des blocs de code suffisamment complets pour éviter les allers-retours avec les labs précédents.

Principes de préparation:

- Pré-fournir DTOs, options, modèles de réponse, contrats de service, exceptions, migrations, helpers de test et configuration `appsettings` dès le starter du lab concerné.
- Garder les signatures de méthodes stables dès le début afin qu'un lab puisse compiler même si certaines méthodes retournent encore `NotImplementedException` ou des réponses temporaires.
- Utiliser les marqueurs LabGen pour remplacer uniquement des blocs courts, pas des fichiers entiers.
- Donner dans les énoncés des snippets complets à coller pour les zones difficiles: configuration DI, middleware, mapping d'erreurs, options Swagger, token validation, telemetry.
- Éviter qu'un lab force à rouvrir un lab précédent: si une dépendance est nécessaire, elle doit être déjà présente dans le starter du lab.

### Fichiers à manipuler par lab

**Lab 1 - Skeleton + Swagger**

Fichiers apprenant:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Pré-fourni:

- `HealthEndpoints.cs` avec metadata déjà en place.
- Packages Swagger dans `DocumentAPI.csproj`.
- Test `SwaggerExposureTests` déjà écrit ou partiellement écrit.

Code à fournir dans le guide:

- Bloc complet `AddEndpointsApiExplorer` / `AddSwaggerGen`.
- Bloc complet `UseSwagger` / `UseSwaggerUI`.
- Exemple complet de `.WithName`, `.WithTags`, `.Produces`.

**Lab 2 - SQL Metadata Persistence**

Fichiers apprenant:

- `src/DocumentAPI/Persistence/DocumentDbContext.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

Pré-fourni:

- `src/DocumentAPI/Entities/Document.cs`
- `DocumentDatabaseOptions.cs`, `DocumentApiOptions.cs` et `appsettings*.json`.
- `DocumentConfiguration.cs`, `PersistenceModelConstants.cs`, `DesignTimeDbContextFactory.cs`.
- Migration `InitialCreate` ou commande unique documentée pour la générer.
- `AzureSqlAuthenticationInterceptor.cs` pour éviter de mélanger SQL et identité managée dans le lab.

Code à fournir dans le guide:

- `DbSet<Document>` et `OnModelCreating`.
- Méthode `AddDocumentServices` avec `AddDbContext`.
- Méthode `InitializeDocumentDatabaseAsync` complète.

**Lab 3 - Blob Storage**

Fichiers apprenant:

- `src/DocumentAPI/Services/Storage/AzureBlobDocumentStorageService.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

Pré-fourni:

- `IDocumentStorageService.cs`.
- `DocumentStorageOptions.cs` et configuration `Storage`.
- Packages Azure Blob et Azure Identity.
- Hash helper si nécessaire.

Code à fournir dans le guide:

- Constructeur complet avec `BlobServiceClient` et `DefaultAzureCredential`.
- Méthodes `SaveAsync`, `OpenReadAsync`, `DeleteAsync` complètes ou avec seulement 1 ou 2 `TODO`.
- Enregistrement DI complet.

**Lab 4 - Upload Happy Path**

Fichiers apprenant:

- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`

Pré-fourni:

- `IDocumentService.cs`.
- `DocumentUploadCommand.cs`, `DocumentDto.cs`, `DocumentMetadataDto.cs`.
- Validation minimale déjà disponible mais non exhaustive.
- `IDocumentStorageService` et `DocumentDbContext` déjà fonctionnels.

Code à fournir dans le guide:

- Handler `UploadAsync` complet côté endpoint.
- Mapping entity vers `DocumentDto`.
- Implémentation complète du happy path `DocumentService.UploadAsync`.

**Lab 5 - Upload Robustesse**

Fichiers apprenant:

- `src/DocumentAPI/Validators/Documents/DocumentUploadValidator.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Pré-fourni:

- `DuplicateDocumentException.cs`.
- `DocumentResiliencePipeline.cs` avec la pipeline Polly prête.
- `RequestValidationFailure.cs`, `DocumentContentTypes.cs`, `UploadOptions.cs`.
- `DocumentStorageIntegrityException.cs`.

Code à fournir dans le guide:

- Validateur complet ou matrice de règles avec snippets.
- Bloc `try/catch` endpoint complet pour `409`, `502`, `503`, `500`.
- Bloc de rollback Blob en cas d'échec SQL.

**Lab 6 - Download + Search**

Fichiers apprenant:

- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Pré-fourni:

- `DocumentSearchCriteria.cs`.
- `DocumentContentResult.cs`.
- Signatures `DownloadAsync` et `SearchAsync` dans `IDocumentService`.

Code à fournir dans le guide:

- Requête EF complète pour `DownloadAsync`.
- Requête EF complète avec filtres optionnels pour `SearchAsync`.
- Handlers endpoint complets pour `/documents/{id}/content` et `/documents/search`.

**Lab 7 - Cache Sur Search**

Fichiers apprenant:

- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Program.cs`

Pré-fourni:

- `SearchOptions.cs`.
- `DocumentSearchCacheVersion.cs`.
- Configuration `Search.CacheTtlSeconds` dans `appsettings`.
- Enregistrement DI du cache version si possible.

Code à fournir dans le guide:

- `AddMemoryCache`.
- Méthode de création de clé déterministe.
- Bloc `GetOrCreateAsync` complet.
- Invalidation après upload réussi.

**Lab 8 - Health Endpoint**

Fichiers apprenant:

- `src/DocumentAPI/Services/Health/DocumentHealthStatusService.cs`
- `src/DocumentAPI/Endpoints/HealthEndpoints.cs`

Pré-fourni:

- Modèles `HealthyOrDegradedStatus`, `UnhealthyStatus`, `HealthCheckStatus`.
- Contrats `IHealthStatusService`, `HealthStatus`, `HealthStateResult`, `HealthDependencyState`.
- Enregistrement DI.

Code à fournir dans le guide:

- Vérification SQL complète.
- Vérification Blob complète.
- Mapping complet `Healthy`, `Degraded`, `Unhealthy` vers HTTP 200/503.

**Lab 9 - Tests Unitaires**

Fichiers apprenant:

- `tests/DocumentAPI.Tests/DocumentServiceTests.cs`
- `tests/DocumentAPI.Tests/DocumentApiEndpointsTests.cs`

Pré-fourni:

- `DocumentApiFactory.cs`.
- `SqlServerFixture.cs`.
- `InMemoryDocumentStorage.cs`.
- Packages de test dans `DocumentAPI.Tests.csproj`.
- `AssemblyInfo.cs` avec `InternalsVisibleTo`.

Code à fournir dans le guide:

- Un test complet upload happy path.
- Un test complet duplicate conflict.
- Un test complet download not found.
- Une table de cas limites à générer avec Copilot.

**Lab 10 - API Versioning**

Fichiers apprenant:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Pré-fourni:

- `ConfigureSwaggerOptions.cs`.
- `SwaggerDefaultValues.cs`.
- Packages `Asp.Versioning`.

Code à fournir dans le guide:

- Bloc complet `AddApiVersioning().AddApiExplorer()`.
- Bloc complet `NewVersionedApi(...).MapGroup(...).HasApiVersion(...)`.
- Bloc Swagger UI multi-version complet.

**Lab 11 - JWT Authentication**

Fichiers apprenant:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Pré-fourni:

- `AuthenticationOptions.cs`.
- Configuration JWT dans `appsettings`.
- Swagger security scheme partiellement préparé si on veut réduire le lab à l'essentiel.
- Helpers de test pour générer un token valide.

Code à fournir dans le guide:

- Bloc complet `AddAuthentication().AddJwtBearer()`.
- `TokenValidationParameters` complet.
- `OnChallenge` complet pour la réponse `401`.
- `.RequireAuthorization()` sur le groupe documents.

**Lab 12 - Observability**

Fichiers apprenant:

- `src/DocumentAPI/Observability/CorrelationIdMiddleware.cs`
- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Services/Monitoring/ApplicationInsightsDocumentActivityMonitor.cs`

Pré-fourni:

- `IDocumentActivityMonitor.cs`.
- `DocumentApiTelemetryInitializer.cs`.
- `ApplicationInsightsMonitoringOptions.cs`.
- Appels au monitor déjà placés dans `DocumentService`.

Code à fournir dans le guide:

- Middleware correlation id complet.
- Bloc `AddHttpLogging` complet.
- Bloc `AddApplicationInsightsTelemetry` complet.
- Méthodes telemetry upload/search/download complètes avec propriétés et métriques.

Diagram de sequence des appels d'API


appsettings.json à ignorer dans la génération
transformer en un simple .cs
Regarder TrustServerCertificate

await EnsureInitializedAsync(cancellationToken); utile toujours au début dans le storage account service ?

Vérifié génération des labs pour les solutions étapes par étapes

 .editorconfig 
 
 rajouter une explication sur _resiliencePipeline

 ajouter quelques lignes sur le logger

 Expliquer plus Polly

 Ajouter lab bonus pour suppression de document avec `DELETE /documents/{id}`

 Mettre lien aka.ms