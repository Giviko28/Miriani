# api — Backend (.NET 10 ASP.NET Core Web API)

The gateway: authentication, roles (RBAC), business data in MS SQL, file intake, and the
single entry point that calls the Python AI service. React talks only to this service.

Built out during **Milestone 2 (weeks 3–4)**. Planned solution layout:

```
api/
├─ Api/             # ASP.NET Core Web API host, controllers, DI, Swagger
├─ Application/     # use cases, DTOs, service interfaces
├─ Domain/          # entities (User, Role, Org, Document, Process, AuditLog)
└─ Infrastructure/  # EF Core (MS SQL), AI-service HTTP client
```

Run:

```bash
dotnet run --project api/Api   # Swagger at /swagger
dotnet test
```
