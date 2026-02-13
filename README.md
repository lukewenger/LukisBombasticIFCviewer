# BombasticIFC Cluster

A Kubernetes-based cluster for hosting and converting IFC (Industry Foundation Classes) models to web-compatible formats.

## Architecture

This project follows **Clean Architecture** principles with **Domain-Driven Design (DDD)**:

- **Domain Layer**: Core business entities, value objects, and repository interfaces
- **Application Layer**: Use cases, DTOs, and application-specific interfaces
- **Infrastructure Layer**: Database context, repository implementations, and external services
- **API Layer**: REST API controllers and presentation logic
- **Shared Layer**: Common constants and utilities

### Key Features

- IFC model upload and storage
- Asynchronous conversion to web-compatible formats (XKT, glTF, GLB)
- RESTful API with Swagger documentation
- PostgreSQL database with Entity Framework Core
- Kubernetes deployment with Minikube
- Docker containerization
- Persistent storage for models and database

## Technology Stack

- **Backend**: .NET 8.0 (C#)
- **Database**: PostgreSQL 16
- **ORM**: Entity Framework Core
- **Architecture**: Clean Architecture + DDD
- **Patterns**: MediatR (CQRS), Repository Pattern
- **Containerization**: Docker
- **Orchestration**: Kubernetes (Minikube)
- **API Documentation**: Swagger/OpenAPI

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started)
- [Minikube](https://minikube.sigs.k8s.io/docs/start/)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)

## Quick Start

### Local Development with Docker Compose

```powershell
# Start the application
docker-compose up -d

# Access the API
# http://localhost:5000/swagger
```

### Kubernetes Deployment with Minikube

```powershell
# Run the deployment script
.\deploy.ps1

# Or on Linux/Mac
chmod +x deploy.sh
./deploy.sh
```

The script will:
1. Start Minikube if not running
2. Enable required addons (Ingress, Storage)
3. Build the Docker image
4. Create storage directories
5. Deploy all Kubernetes resources
6. Display access information

## Project Structure

```
BombasticIFCcluster/
├── src/
│   ├── BombasticIFC.Domain/          # Core business logic
│   │   ├── Entities/                 # Domain entities
│   │   ├── ValueObjects/             # Value objects
│   │   ├── Enums/                    # Enumerations
│   │   └── Repositories/             # Repository interfaces
│   ├── BombasticIFC.Application/     # Application layer
│   │   ├── UseCases/                 # Business use cases
│   │   ├── DTOs/                     # Data transfer objects
│   │   └── Common/Interfaces/        # Application interfaces
│   ├── BombasticIFC.Infrastructure/  # Infrastructure layer
│   │   ├── Persistence/              # Database context
│   │   ├── Repositories/             # Repository implementations
│   │   └── Services/                 # External services
│   ├── BombasticIFC.API/             # Presentation layer
│   │   └── Controllers/              # API controllers
│   └── BombasticIFC.Shared/          # Shared utilities
├── kubernetes/                        # Kubernetes manifests
│   ├── namespace.yaml
│   ├── secrets.yaml
│   ├── postgres-deployment.yaml
│   ├── api-deployment.yaml
│   └── ingress.yaml
├── Dockerfile                         # Docker image definition
├── docker-compose.yml                 # Docker Compose configuration
├── deploy.sh                          # Linux/Mac deployment script
└── deploy.ps1                         # Windows deployment script
```

## API Endpoints

### Models

- `POST /api/models/upload` - Upload a new IFC model
- `GET /api/models/{id}` - Get model by ID

### Conversions

- `POST /api/conversions` - Create a conversion job
- `GET /api/conversions/{id}` - Get conversion job status

### Health

- `GET /health` - Health check endpoint

## Accessing Services

After deployment, services are accessible at:

- **API (NodePort)**: `http://<minikube-ip>:30080`
- **Swagger UI**: `http://<minikube-ip>:30080/swagger`
- **PostgreSQL**: `<minikube-ip>:30432`

Get Minikube IP:
```powershell
minikube ip
```

### Using Ingress

Add to your hosts file:
```
<minikube-ip> bombasticifccluster.local
```

Then access: `http://bombasticifccluster.local`

## Development

### Build the Solution

```powershell
dotnet build BombasticIFCcluster.sln
```

### Run Tests

```powershell
dotnet test
```

### Database Migrations

```powershell
cd src/BombasticIFC.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../BombasticIFC.API
dotnet ef database update --startup-project ../BombasticIFC.API
```

## Clean Code Principles Applied

1. **Single Responsibility Principle**: Each class has one reason to change
2. **Dependency Inversion**: High-level modules don't depend on low-level modules
3. **Separation of Concerns**: Clear layer boundaries
4. **Explicit Dependencies**: Constructor injection
5. **Immutability**: Private setters, factory methods
6. **Meaningful Names**: Clear, descriptive names for classes and methods
7. **Domain-Driven Design**: Rich domain models with business logic

## Monitoring

### View Cluster Status

```powershell
kubectl get all -n bombasticifccluster
```

### View Logs

```powershell
# API logs
kubectl logs -f deployment/api-deployment -n bombasticifccluster

# PostgreSQL logs
kubectl logs -f deployment/postgres-deployment -n bombasticifccluster
```

### Minikube Dashboard

```powershell
minikube dashboard
```

## Cleanup

### Stop Minikube

```powershell
minikube stop
```

### Delete Cluster

```powershell
minikube delete
```

### Stop Docker Compose

```powershell
docker-compose down -v
```

## Contributing

This project follows Clean Code and SOLID principles. When contributing:

1. Follow the existing architecture patterns
2. Write unit tests for new features
3. Use meaningful commit messages
4. Document public APIs

## License

[Specify your license here]

## Author

Lukas Wenger

## Project Context

Part of PROG3 - Advanced Programming Course
Supervisor: Reto Glarner