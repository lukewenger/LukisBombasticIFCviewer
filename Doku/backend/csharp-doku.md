# BombasticIFC - C# Backend Documentation

**Version:** 1.0  
**Framework:** .NET 8.0  
**Architecture:** Clean Architecture with CQRS  
**Database:** PostgreSQL  
**Last Updated:** March 7, 2026

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Domain Layer](#domain-layer)
5. [Application Layer](#application-layer)
6. [Infrastructure Layer](#infrastructure-layer)
7. [API Layer](#api-layer)
8. [Shared Layer](#shared-layer)
9. [Database Design](#database-design)
10. [API Endpoints](#api-endpoints)
11. [Authentication & Authorization](#authentication--authorization)
12. [Design Patterns](#design-patterns)
13. [Development Setup](#development-setup)
14. [Configuration](#configuration)
15. [Future Enhancements](#future-enhancements)

---

## Project Overview

BombasticIFC is a modern C# backend application for managing IFC (Industry Foundation Classes) files and their conversions. The system provides functionality for:

- **User Authentication** - JWT-based authentication with role management
- **IFC File Management** - Upload, store, and track IFC building models
- **Format Conversion** - Convert IFC files to web-friendly formats (XKT, GLTF, GLB, JSON)
- **Version Control** - Track multiple versions of models
- **Async Processing** - Background job processing for conversions

### Key Features

✅ RESTful API with OpenAPI/Swagger documentation  
✅ JWT Bearer authentication with refresh tokens  
✅ PostgreSQL database with Entity Framework Core  
✅ CQRS pattern with MediatR for clean request handling  
✅ Repository pattern for data access abstraction  
✅ Soft delete support for data retention  
✅ Value objects for domain-driven design  
✅ Docker-ready configuration  
✅ Kubernetes deployment support  

---

## Architecture

### Clean Architecture Overview

The project follows **Clean Architecture** (also known as Onion Architecture) with strict separation of concerns:

```
┌─────────────────────────────────────────────────┐
│          API Layer (Controllers)                │
│         ↓ depends on ↓                          │
├─────────────────────────────────────────────────┤
│      Application Layer (Use Cases/CQRS)         │
│         ↓ depends on ↓                          │
├─────────────────────────────────────────────────┤
│       Domain Layer (Business Logic)             │ ← Core (No Dependencies)
│         ↑ implemented by ↑                      │
├─────────────────────────────────────────────────┤
│   Infrastructure Layer (External Concerns)      │
└─────────────────────────────────────────────────┘
```

**Dependency Rule:** Dependencies point inwards. The Domain layer has zero external dependencies.

### Architecture Diagram

See [csharp-architecture.puml](./csharp-architecture.puml) for the full PlantUML diagram showing:
- Layer dependencies
- Component relationships
- CQRS flow
- External system integration

---

## Project Structure

```
src/
├── BombasticIFC.API/                # Presentation Layer
│   ├── Controllers/                 # REST API Controllers
│   ├── Program.cs                   # Application entry point
│   ├── appsettings.json             # Production configuration
│   └── appsettings.Development.json # Development configuration
│
├── BombasticIFC.Application/        # Use Cases Layer
│   ├── Authentication/              # Auth commands/queries
│   │   ├── RegisterCommand.cs
│   │   ├── LoginCommand.cs
│   │   └── GetCurrentUserQuery.cs
│   ├── Models/                      # Model management
│   │   ├── UploadModelCommand.cs
│   │   ├── GetModelsQuery.cs
│   │   └── GetModelByIdQuery.cs
│   ├── Conversions/                 # Conversion jobs
│   │   ├── CreateConversionJobCommand.cs
│   │   └── GetConversionJobQuery.cs
│   ├── Common/
│   │   ├── Interfaces/              # Service abstractions
│   │   └── Mappings/                # AutoMapper profiles
│   └── DTOs/                        # Data Transfer Objects
│
├── BombasticIFC.Domain/             # Core Business Logic
│   ├── Entities/                    # Domain entities
│   │   ├── BaseEntity.cs
│   │   ├── User.cs
│   │   ├── IfcModel.cs
│   │   ├── ConversionJob.cs
│   │   └── ModelVersion.cs
│   ├── ValueObjects/                # Immutable value objects
│   │   └── ModelMetadata.cs
│   ├── Enums/                       # Domain enumerations
│   │   ├── UserRole.cs
│   │   ├── ModelStatus.cs
│   │   ├── ConversionStatus.cs
│   │   └── ConversionFormat.cs
│   └── Repositories/                # Repository interfaces
│       ├── IUserRepository.cs
│       ├── IIfcModelRepository.cs
│       └── IConversionJobRepository.cs
│
├── BombasticIFC.Infrastructure/     # External Concerns
│   ├── Data/
│   │   ├── ApplicationDbContext.cs  # EF Core DbContext
│   │   ├── Configurations/          # Fluent API configurations
│   │   └── Migrations/              # Database migrations
│   ├── Repositories/                # Repository implementations
│   ├── Services/                    # Service implementations
│   │   ├── TokenService.cs
│   │   ├── PasswordHasherService.cs
│   │   └── FileStorageService.cs
│   └── Seeding/
│       └── DatabaseSeeder.cs        # Sample data seeding
│
└── BombasticIFC.Shared/             # Cross-Cutting Concerns
    └── Constants/
        └── AppConstants.cs          # Application constants
```

---

## Domain Layer

### 🎯 Purpose
Contains core business logic, entities, value objects, and domain rules. **Zero external dependencies**.

### Core Entities

#### 1. **BaseEntity** (Abstract)
Base class for all entities providing common properties:

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; }
    
    public void MarkAsUpdated() // Update timestamp
    public void MarkAsDeleted() // Soft delete
}
```

#### 2. **User**
Represents system users with authentication and authorization.

**Properties:**
- `Username` (string, unique)
- `Email` (string, unique)
- `PasswordHash` (string)
- `Role` (UserRole enum)
- `IsActive` (bool)

**Navigation:**
- `ICollection<IfcModel> Models` - User's uploaded models

**Factory Method:**
```csharp
public static User Create(string username, string email, string passwordHash, UserRole role)
```

#### 3. **IfcModel**
Core entity representing IFC building model files.

**Properties:**
- `FileName` (string)
- `OriginalFilePath` (string)
- `FileSizeBytes` (long)
- `Metadata` (ModelMetadata value object)
- `Status` (ModelStatus enum)
- `UserId` (Guid)

**Navigation:**
- `User User` - Owner
- `ICollection<ConversionJob> ConversionJobs` - Related conversions
- `ICollection<ModelVersion> Versions` - Version history

**Methods:**
```csharp
public void UpdateMetadata(ModelMetadata metadata)
public void UpdateStatus(ModelStatus status)
public ConversionJob CreateConversionJob(ConversionFormat targetFormat)
public ModelVersion CreateVersion(int versionNumber, string description)
```

#### 4. **ConversionJob**
Represents asynchronous conversion tasks.

**Properties:**
- `ModelId` (Guid)
- `TargetFormat` (ConversionFormat enum)
- `Status` (ConversionStatus enum)
- `ProgressPercentage` (int, 0-100)
- `StartedAt` (DateTime?)
- `CompletedAt` (DateTime?)
- `OutputFilePath` (string?)
- `ErrorMessage` (string?)

**Methods:**
```csharp
public void StartProcessing()           // Set status to Processing, record start time
public void UpdateProgress(int percent) // Update progress (0-100)
public void Complete(string outputPath) // Mark as completed with output file
public void Fail(string errorMessage)   // Mark as failed with error details
public void Cancel()                    // Mark as cancelled
```

**Business Rules:**
- Progress must be 0-100
- Cannot update progress if not Processing
- Cannot complete without output path
- Cannot start if already processing

#### 5. **ModelVersion**
Tracks version history for models.

**Properties:**
- `ModelId` (Guid)
- `VersionNumber` (int)
- `Description` (string)
- `FilePath` (string)
- `IsActive` (bool)

**Methods:**
```csharp
public void SetFilePath(string path)
public void Deactivate()
```

### Value Objects

#### **ModelMetadata**
Immutable value object with structural equality.

```csharp
public class ModelMetadata : ValueObject
{
    public string IfcSchema { get; private set; }           // e.g., "IFC2X3"
    public string? ProjectName { get; private set; }
    public string? ApplicationName { get; private set; }
    public int NumberOfElements { get; private set; }
    public Dictionary<string, int> ElementTypeCounts { get; private set; }
    
    public static ModelMetadata Create(...)
    public static ModelMetadata Empty { get; } // Default empty metadata
}
```

**Example:**
```json
{
  "IfcSchema": "IFC2X3",
  "ProjectName": "Duplex Building",
  "NumberOfElements": 1234,
  "ElementTypeCounts": {
    "IfcWall": 45,
    "IfcWindow": 20,
    "IfcDoor": 15
  }
}
```

### Enumerations

#### **UserRole**
```csharp
public enum UserRole
{
    User = 0,           // Regular user (upload, convert)
    Administrator = 1,  // Full access
    Viewer = 2          // Read-only access
}
```

#### **ModelStatus**
```csharp
public enum ModelStatus
{
    Uploaded = 0,    // Initial upload complete
    Processing = 1,  // Being processed/analyzed
    Ready = 2,       // Available for use
    Failed = 3,      // Processing failed
    Archived = 4     // Soft-archived
}
```

#### **ConversionStatus**
```csharp
public enum ConversionStatus
{
    Queued = 0,      // Waiting to be processed
    Processing = 1,   // Currently converting
    Completed = 2,    // Successfully completed
    Failed = 3,       // Conversion failed
    Cancelled = 4     // User cancelled
}
```

#### **ConversionFormat**
```csharp
public enum ConversionFormat
{
    XKT = 0,   // XKT format for xeokit viewer
    GLTF = 1,  // glTF 2.0 (JSON)
    GLB = 2,   // glTF 2.0 (Binary)
    JSON = 3   // Custom JSON format
}
```

### Repository Interfaces

Defined in Domain, implemented in Infrastructure.

#### **IUserRepository**
```csharp
Task<User?> GetByIdAsync(Guid id);
Task<User?> GetByUsernameAsync(string username);
Task<User?> GetByEmailAsync(string email);
Task<bool> ExistsAsync(Guid id);
Task AddAsync(User user);
Task UpdateAsync(User user);
Task DeleteAsync(Guid id);
```

#### **IIfcModelRepository**
```csharp
Task<IfcModel?> GetByIdAsync(Guid id);
Task<IEnumerable<IfcModel>> GetAllAsync();
Task<IEnumerable<IfcModel>> GetByUserIdAsync(Guid userId);
Task AddAsync(IfcModel model);
Task UpdateAsync(IfcModel model);
Task DeleteAsync(Guid id);
```

#### **IConversionJobRepository**
```csharp
Task<ConversionJob?> GetByIdAsync(Guid id);
Task<IEnumerable<ConversionJob>> GetByModelIdAsync(Guid modelId);
Task<IEnumerable<ConversionJob>> GetByStatusAsync(ConversionStatus status);
Task AddAsync(ConversionJob job);
Task UpdateAsync(ConversionJob job);
```

---

## Application Layer

### 🎯 Purpose
Orchestrates use cases using CQRS pattern with MediatR. Contains commands (write), queries (read), and DTOs.

### Commands & Queries

#### **Authentication Use Cases**

##### 1. **RegisterCommand**
Registers a new user in the system.

```csharp
public record RegisterCommand(string Username, string Email, string Password) 
    : IRequest<Result<AuthResponseDto>>;
```

**Handler Logic:**
1. Validate username/email uniqueness
2. Hash password using BCrypt
3. Create User entity with role
4. Persist to database
5. Generate JWT access token + refresh token
6. Return AuthResponseDto

**Returns:**
```csharp
public record AuthResponseDto(
    Guid UserId,
    string Username,
    string Email,
    string Role,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
```

##### 2. **LoginCommand**
Authenticates existing user.

```csharp
public record LoginCommand(string UsernameOrEmail, string Password) 
    : IRequest<Result<AuthResponseDto>>;
```

**Handler Logic:**
1. Find user by username OR email
2. Verify password hash
3. Check if user is active
4. Generate new tokens
5. Return AuthResponseDto

##### 3. **GetCurrentUserQuery**
Retrieves authenticated user profile.

```csharp
public record GetCurrentUserQuery(Guid UserId) 
    : IRequest<Result<UserProfileDto>>;
```

**Returns:**
```csharp
public record UserProfileDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);
```

#### **Model Management Use Cases**

##### 4. **UploadModelCommand**
Handles IFC file upload.

```csharp
public record UploadModelCommand(
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    Guid UserId
) : IRequest<Result<IfcModelDto>>;
```

**Handler Logic:**
1. Validate file extension (.ifc)
2. Validate file size (max 500 MB)
3. Save file to storage using IFileStorageService
4. Parse IFC metadata (if available)
5. Create IfcModel entity
6. Persist to database
7. Return IfcModelDto

##### 5. **GetModelsQuery**
Retrieves all models (optionally filtered by user).

```csharp
public record GetModelsQuery(Guid? UserId = null) 
    : IRequest<Result<IEnumerable<IfcModelDto>>>;
```

##### 6. **GetModelByIdQuery**
Retrieves specific model with full details.

```csharp
public record GetModelByIdQuery(Guid ModelId) 
    : IRequest<Result<IfcModelDto>>;
```

**Returns:**
```csharp
public class IfcModelDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public ModelStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ModelMetadataDto? Metadata { get; set; }
    public List<ConversionJobDto> ConversionJobs { get; set; }
}
```

#### **Conversion Use Cases**

##### 7. **CreateConversionJobCommand**
Creates a new conversion job.

```csharp
public record CreateConversionJobCommand(
    Guid ModelId,
    ConversionFormat TargetFormat
) : IRequest<Result<ConversionJobDto>>;
```

**Handler Logic:**
1. Load IfcModel entity
2. Validate model exists and is Ready
3. Call domain method: `model.CreateConversionJob(format)`
4. Persist job to database
5. (Future: Trigger background worker)
6. Return ConversionJobDto

##### 8. **GetConversionJobQuery**
Retrieves job status and progress.

```csharp
public record GetConversionJobQuery(Guid JobId) 
    : IRequest<Result<ConversionJobDto>>;
```

**Returns:**
```csharp
public class ConversionJobDto
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public ConversionFormat TargetFormat { get; set; }
    public ConversionStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? OutputFilePath { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Service Interfaces

Defined in Application, implemented in Infrastructure.

#### **IApplicationDbContext**
```csharp
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

#### **ITokenService**
```csharp
public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
```

#### **IPasswordHasherService**
```csharp
public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
```

#### **IFileStorageService**
```csharp
public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName);
    Task<Stream?> GetFileAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task<long> GetFileSizeAsync(string filePath);
}
```

#### **IIfcConversionService**
```csharp
public interface IIfcConversionService
{
    Task<ConversionResult> ConvertAsync(string inputPath, ConversionFormat format);
    Task<bool> IsConversionSupportedAsync(ConversionFormat format);
}
```
*(Implementation pending - future feature)*

---

## Infrastructure Layer

### 🎯 Purpose
Implements interfaces from Domain and Application layers. Handles external concerns like database, file system, authentication.

### Database Context

#### **ApplicationDbContext**
EF Core DbContext with PostgreSQL provider.

```csharp
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<IfcModel> IfcModels { get; set; }
    public DbSet<ConversionJob> ConversionJobs { get; set; }
    public DbSet<ModelVersion> ModelVersions { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        
        // Global query filters for soft delete
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<IfcModel>().HasQueryFilter(m => !m.IsDeleted);
    }
}
```

**Features:**
- Soft delete query filters (excludes deleted entities automatically)
- Automatic configuration discovery
- PostgreSQL-specific features (Npgsql provider)
- Connection string from appsettings.json

### Entity Configurations

Fluent API configurations for precise schema control.

#### **IfcModelConfiguration**
```csharp
public class IfcModelConfiguration : IEntityTypeConfiguration<IfcModel>
{
    public void Configure(EntityTypeBuilder<IfcModel> builder)
    {
        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.FileName)
            .IsRequired()
            .HasMaxLength(500);
            
        builder.Property(m => m.FileSizeBytes)
            .IsRequired();
        
        // Owned entity for Metadata (stored as JSON)
        builder.OwnsOne(m => m.Metadata, metadata =>
        {
            metadata.Property(md => md.IfcSchema).HasMaxLength(50);
            metadata.Property(md => md.ProjectName).HasMaxLength(500);
            metadata.OwnsOne(md => md.ElementTypeCounts); // Stored as JSON
        });
        
        // Indexes for performance
        builder.HasIndex(m => m.FileName);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.CreatedAt);
        
        // Relationships with cascade delete
        builder.HasMany(m => m.ConversionJobs)
            .WithOne(j => j.Model)
            .HasForeignKey(j => j.ModelId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(m => m.Versions)
            .WithOne(v => v.Model)
            .HasForeignKey(v => v.ModelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Key Features:**
- Metadata stored as JSON using EF Core 8.0 owned entities
- Performance indexes on frequently queried columns
- Cascade deletes for child entities
- String length constraints

### Repository Implementations

Concrete implementations of domain repository interfaces.

#### **IfcModelRepository**
```csharp
public class IfcModelRepository : IIfcModelRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<IfcModel?> GetByIdAsync(Guid id)
    {
        return await _context.IfcModels
            .Include(m => m.ConversionJobs)  // Eager load
            .Include(m => m.Versions)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
    
    public async Task<IEnumerable<IfcModel>> GetByUserIdAsync(Guid userId)
    {
        return await _context.IfcModels
            .Where(m => m.UserId == userId)
            .Include(m => m.ConversionJobs)
            .ToListAsync();
    }
    
    // ... other CRUD operations
}
```

**Features:**
- Eager loading of navigation properties
- Soft delete awareness (via query filters)
- Async/await throughout
- LINQ query optimization

### Service Implementations

#### **TokenService**
JWT token generation with configurable settings.

```csharp
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    
    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
```

**Configuration (appsettings.json):**
```json
{
  "JwtSettings": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "BombasticIFC",
    "Audience": "BombasticIFC-Client",
    "ExpirationMinutes": 60
  }
}
```

#### **PasswordHasherService**
BCrypt-based password hashing.

```csharp
public class PasswordHasherService : IPasswordHasherService
{
    private const int WorkFactor = 12; // BCrypt cost factor
    
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }
    
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

**Security:**
- Work factor 12 (2^12 = 4096 iterations)
- Salted and hashed
- Resistant to rainbow table attacks

#### **FileStorageService**
Local filesystem storage (can be replaced with cloud storage).

```csharp
public class FileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var fullPath = Path.Combine(_storagePath, uniqueFileName);
        
        await using var fileStreamOutput = File.Create(fullPath);
        await fileStream.CopyToAsync(fileStreamOutput);
        
        return uniqueFileName;
    }
    
    public async Task<Stream?> GetFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_storagePath, filePath);
        if (!File.Exists(fullPath)) return null;
        
        var memory = new MemoryStream();
        await using var fileStream = File.OpenRead(fullPath);
        await fileStream.CopyToAsync(memory);
        memory.Position = 0;
        return memory;
    }
    
    // ... other file operations
}
```

**Configuration:**
```json
{
  "StoragePath": "./data/storage"  // Development
  "StoragePath": "/data/storage"   // Docker/Production
}
```

### Database Seeding

Sample data for development/demo environments.

```csharp
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if already seeded
        if (await context.IfcModels.AnyAsync()) return;
        
        // Seed sample IFC model
        var sampleModel = IfcModel.Create(
            fileName: "Duplex.ifc",
            filePath: "seed-data/Duplex.ifc",
            fileSizeBytes: 123456,
            userId: Guid.NewGuid()
        );
        
        sampleModel.UpdateMetadata(ModelMetadata.Create(
            ifcSchema: "IFC2X3",
            projectName: "Duplex House",
            numberOfElements: 1234
        ));
        
        sampleModel.UpdateStatus(ModelStatus.Ready);
        
        // Seed completed conversion job
        var conversionJob = sampleModel.CreateConversionJob(ConversionFormat.XKT);
        conversionJob.StartProcessing();
        conversionJob.Complete("seed-data/Duplex.xkt");
        
        context.IfcModels.Add(sampleModel);
        await context.SaveChangesAsync();
    }
}
```

---

## API Layer

### 🎯 Purpose
Exposes HTTP REST API using ASP.NET Core. Handles request/response, authentication, and dependency injection.

### Controllers

All controllers inherit from `ControllerBase` and use `[ApiController]` attribute for automatic model validation.

#### **AuthController** (`/api/auth`)

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterCommand command)
    
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginCommand command)
    
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser()
}
```

**Endpoints:**
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Authenticate user
- `GET /api/auth/me` - Get current user (requires JWT)

#### **ModelsController** (`/api/models`)

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModelsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IfcModelDto>>> GetModels()
    
    [HttpPost("upload")]
    public async Task<ActionResult<IfcModelDto>> UploadModel(IFormFile file)
    
    [HttpGet("{id}")]
    public async Task<ActionResult<IfcModelDto>> GetModel(Guid id)
    
    [HttpGet("{id}/output")]
    public async Task<IActionResult> DownloadOutput(Guid id, [FromQuery] ConversionFormat format)
}
```

**Endpoints:**
- `GET /api/models` - List all models
- `POST /api/models/upload` - Upload IFC file
- `GET /api/models/{id}` - Get model details
- `GET /api/models/{id}/output?format=XKT` - Download converted file

**Upload Validation:**
```csharp
[HttpPost("upload")]
public async Task<ActionResult<IfcModelDto>> UploadModel(IFormFile file)
{
    // Validate file extension
    if (!file.FileName.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
        return BadRequest("Only .ifc files are allowed");
    
    // Validate file size
    if (file.Length > AppConstants.MaxFileSize)
        return BadRequest($"File size exceeds {AppConstants.MaxFileSize} bytes");
    
    // Process upload...
}
```

#### **ConversionsController** (`/api/conversions`)

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversionsController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ConversionJobDto>> CreateConversion(CreateConversionJobCommand command)
    
    [HttpGet("{id}")]
    public async Task<ActionResult<ConversionJobDto>> GetConversionJob(Guid id)
}
```

**Endpoints:**
- `POST /api/conversions` - Create conversion job
- `GET /api/conversions/{id}` - Get job status

**Request Example:**
```json
{
  "modelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetFormat": 0  // 0=XKT, 1=GLTF, 2=GLB, 3=JSON
}
```

### Startup Configuration (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add MediatR for CQRS
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

// Add Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IIfcModelRepository, IfcModelRepository>();
builder.Services.AddScoped<IConversionJobRepository, ConversionJobRepository>();

// Add Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    });

// Add CORS (for frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

// Middleware pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## Shared Layer

### AppConstants

Application-wide constants.

```csharp
public static class AppConstants
{
    public const string ApplicationName = "BombasticIFC Cluster";
    public const string ApiVersion = "v1";
    
    // File handling
    public static readonly string[] AllowedFileExtensions = { ".ifc" };
    public static readonly string[] SupportedOutputFormats = { ".xkt", ".gltf", ".glb", ".json" };
    
    // Limits
    public const long MaxFileSize = 500 * 1024 * 1024; // 500 MB
    public const int MaxConcurrentConversions = 5;
    
    // Validation
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 50;
    public const int MinPasswordLength = 8;
}
```

---

## Database Design

### Entity Relationship Diagram

```
┌─────────────────┐
│     Users       │
├─────────────────┤
│ Id (PK)         │
│ Username        │
│ Email           │
│ PasswordHash    │
│ Role            │
│ IsActive        │
│ CreatedAt       │
│ UpdatedAt       │
│ IsDeleted       │
└─────────┬───────┘
          │ 1
          │
          │ n
┌─────────▼───────────────┐
│     IfcModels           │
├─────────────────────────┤
│ Id (PK)                 │
│ FileName                │
│ OriginalFilePath        │
│ FileSizeBytes           │
│ Status                  │
│ UserId (FK)             │
│ CreatedAt               │
│ UpdatedAt               │
│ IsDeleted               │
│ Metadata (JSON)         │
└─────┬───────────┬───────┘
      │ 1         │ 1
      │           │
      │ n         │ n
┌─────▼───────────┐  ┌────▼─────────────────┐
│ ConversionJobs  │  │   ModelVersions      │
├─────────────────┤  ├──────────────────────┤
│ Id (PK)         │  │ Id (PK)              │
│ ModelId (FK)    │  │ ModelId (FK)         │
│ TargetFormat    │  │ VersionNumber        │
│ Status          │  │ Description          │
│ Progress        │  │ FilePath             │
│ StartedAt       │  │ IsActive             │
│ CompletedAt     │  │ CreatedAt            │
│ OutputFilePath  │  └──────────────────────┘
│ ErrorMessage    │
│ CreatedAt       │
└─────────────────┘
```

### Indexes

**IfcModels:**
- `IX_IfcModels_FileName` - Fast filename lookups
- `IX_IfcModels_Status` - Filter by status
- `IX_IfcModels_CreatedAt` - Chronological sorting
- `IX_IfcModels_UserId` - User's models lookup

**Users:**
- `IX_Users_Username` (Unique) - Authentication
- `IX_Users_Email` (Unique) - Email login

**ConversionJobs:**
- `IX_ConversionJobs_ModelId` - Model's jobs
- `IX_ConversionJobs_Status` - Queue management

### Sample Data

Initial seed includes:
- Sample "Duplex.ifc" model with metadata
- Completed XKT conversion job
- Sample XKT output file

---

## API Endpoints

### Base URL
- **Development:** `http://localhost:5000`
- **Production:** `https://api.bombasticif c.example.com`

### Authentication Endpoints

#### Register User
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john@example.com",
  "password": "SecurePass123!"
}

Response 200:
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "john.doe",
  "email": "john@example.com",
  "role": "User",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "xWzQ3NzE4MTAy...",
  "expiresAt": "2026-03-07T15:30:00Z"
}
```

#### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "usernameOrEmail": "john.doe",
  "password": "SecurePass123!"
}

Response 200: (Same as register)
```

#### Get Current User
```http
GET /api/auth/me
Authorization: Bearer {token}

Response 200:
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "john.doe",
  "email": "john@example.com",
  "role": "User",
  "isActive": true,
  "createdAt": "2026-03-01T10:00:00Z"
}
```

### Model Endpoints

#### Upload Model
```http
POST /api/models/upload
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [Duplex.ifc binary data]

Response 201:
{
  "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "Duplex.ifc",
  "fileSizeBytes": 1234567,
  "status": "Uploaded",
  "createdAt": "2026-03-07T14:30:00Z",
  "updatedAt": "2026-03-07T14:30:00Z",
  "metadata": null,
  "conversionJobs": []
}
```

#### List Models
```http
GET /api/models
Authorization: Bearer {token}

Response 200:
[
  {
    "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "fileName": "Duplex.ifc",
    "fileSizeBytes": 1234567,
    "status": "Ready",
    "createdAt": "2026-03-07T14:30:00Z",
    "metadata": {
      "ifcSchema": "IFC2X3",
      "projectName": "Duplex House",
      "numberOfElements": 1234,
      "elementTypeCounts": {
        "IfcWall": 45,
        "IfcWindow": 20
      }
    }
  }
]
```

#### Get Model Details
```http
GET /api/models/{id}
Authorization: Bearer {token}

Response 200: (Single model with conversion jobs)
```

#### Download Converted File
```http
GET /api/models/{id}/output?format=XKT
Authorization: Bearer {token}

Response 200:
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="Duplex.xkt"

[Binary file data]
```

### Conversion Endpoints

#### Create Conversion Job
```http
POST /api/conversions
Authorization: Bearer {token}
Content-Type: application/json

{
  "modelId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetFormat": 0  // XKT
}

Response 201:
{
  "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "modelId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetFormat": "XKT",
  "status": "Queued",
  "progressPercentage": 0,
  "startedAt": null,
  "completedAt": null,
  "outputFilePath": null,
  "errorMessage": null
}
```

#### Get Conversion Job Status
```http
GET /api/conversions/{id}
Authorization: Bearer {token}

Response 200:
{
  "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "modelId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetFormat": "XKT",
  "status": "Completed",
  "progressPercentage": 100,
  "startedAt": "2026-03-07T14:35:00Z",
  "completedAt": "2026-03-07T14:37:30Z",
  "outputFilePath": "outputs/9fa85f64_Duplex.xkt",
  "errorMessage": null
}
```

---

## Authentication & Authorization

### JWT Token Structure

**Claims:**
```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // User ID
  "unique_name": "john.doe",                      // Username
  "email": "john@example.com",                    // Email
  "role": "User",                                 // Role
  "jti": "random-guid",                           // Token ID
  "iss": "BombasticIFC",                         // Issuer
  "aud": "BombasticIFC-Client",                   // Audience
  "exp": 1709825400,                              // Expiration (Unix timestamp)
  "iat": 1709821800                               // Issued at
}
```

### Authorization Policies

**Role-Based:**
- `[Authorize]` - Requires any authenticated user
- `[Authorize(Roles = "Administrator")]` - Admin only
- `[Authorize(Roles = "User,Administrator")]` - Multiple roles

**Custom Claims:**
```csharp
[Authorize(Policy = "CanUploadModels")]
```

### Security Best Practices

✅ **Password Security:**
- BCrypt with work factor 12
- Minimum 8 characters
- Salted and hashed

✅ **Token Security:**
- Short-lived access tokens (60 min)
- Secure secret key (min 32 chars)
- HTTPS only in production

✅ **API Security:**
- CORS configured for frontend
- JWT Bearer validation
- Model binding validation
- File upload validation

---

## Design Patterns

### Implemented Patterns

| Pattern | Location | Purpose |
|---------|----------|---------|
| **Clean Architecture** | Overall structure | Separation of concerns, testability |
| **CQRS** | Application layer | Separate read/write operations |
| **Mediator** | Application layer | Decouple request/handler logic |
| **Repository** | Domain/Infrastructure | Abstract data access |
| **Factory Method** | Domain entities | Controlled entity creation |
| **Value Object** | Domain | Immutable, value-based equality |
| **Soft Delete** | Domain/Infrastructure | Data retention, audit trail |
| **Dependency Injection** | All layers | Loose coupling, testability |
| **Unit of Work** | DbContext | Transaction management |
| **Strategy** | Conversion formats | Pluggable conversion strategies |

### CQRS Flow Example

```
Client Request
     ↓
Controller (API Layer)
     ↓
Sends Command/Query (MediatR)
     ↓
Handler (Application Layer)
     ↓
Uses Repository (Domain Interface)
     ↓
Repository Implementation (Infrastructure)
     ↓
Database Context (EF Core)
     ↓
PostgreSQL
```

---

## Development Setup

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL 15+
- Docker (optional)
- VS Code or Visual Studio 2022

### Local Development

1. **Clone Repository:**
```bash
git clone https://github.com/yourorg/BombasticIFCcluster.git
cd BombasticIFCcluster/src
```

2. **Configure Database:**
```bash
# Update appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=bombasticifcdb;Username=postgres;Password=yourpassword"
  }
}
```

3. **Run Migrations:**
```bash
cd BombasticIFC.Infrastructure
dotnet ef database update --startup-project ../BombasticIFC.API
```

4. **Run API:**
```bash
cd ../BombasticIFC.API
dotnet run
```

5. **Access Swagger:**
```
http://localhost:5000/swagger
```

### Docker Development

```bash
# Build and run all services
docker-compose up -d

# View logs
docker-compose logs -f api

# Stop services
docker-compose down
```

### Running Tests

```bash
# Unit tests
dotnet test

# Integration tests
dotnet test --filter Category=Integration

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=opencover
```

---

## Configuration

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=bombasticifcdb;Username=postgres;Password=dev123"
  },
  "JwtSettings": {
    "Secret": "your-secret-key-must-be-at-least-32-characters-long",
    "Issuer": "BombasticIFC",
    "Audience": "BombasticIFC-Client",
    "ExpirationMinutes": 60
  },
  "StoragePath": "./data/storage",
  "SeedSampleData": true
}
```

### appsettings.json (Production)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=bombasticifcdb;Username=${DB_USER};Password=${DB_PASSWORD}"
  },
  "JwtSettings": {
    "Secret": "${JWT_SECRET}",
    "Issuer": "BombasticIFC",
    "Audience": "BombasticIFC-Client",
    "ExpirationMinutes": 120
  },
  "StoragePath": "/data/storage",
  "SeedSampleData": false
}
```

### Environment Variables (Docker/Kubernetes)

```bash
DB_USER=postgres
DB_PASSWORD=SecurePassword123!
JWT_SECRET=your-production-secret-key-min-32-chars
```

---

## Future Enhancements

### Planned Features

1. **Background Processing**
   - Implement `IIfcConversionService` with Hangfire/Quartz
   - Queue management for conversion jobs
   - Progress tracking with SignalR

2. **Cloud Storage**
   - Azure Blob Storage implementation
   - AWS S3 implementation
   - Pluggable storage providers

3. **Advanced Features**
   - Model comparison/diffing
   - Collaborative editing
   - Real-time notifications
   - Audit logging

4. **Performance**
   - Redis caching layer
   - Pagination for large datasets
   - Background thumbnail generation
   - CDN integration

5. **Testing**
   - Unit test coverage (target 80%+)
   - Integration tests
   - E2E API tests
   - Performance tests

6. **Documentation**
   - XML documentation comments
   - Postman collection
   - Interactive API docs
   - Architecture decision records (ADRs)

---

## Appendix

### NuGet Packages by Project

**BombasticIFC.API:**
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.1
- Swashbuckle.AspNetCore 6.5.0
- MediatR 12.2.0

**BombasticIFC.Application:**
- FluentValidation 11.9.0
- MediatR 12.2.0

**BombasticIFC.Infrastructure:**
- Microsoft.EntityFrameworkCore 8.0.1
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0
- BCrypt.Net-Next 4.0.3
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.1

**BombasticIFC.Domain:**
- *(No external dependencies)*

**BombasticIFC.Shared:**
- *(No external dependencies)*

### Useful Commands

```bash
# Create migration
dotnet ef migrations add MigrationName --startup-project ../BombasticIFC.API

# Update database
dotnet ef database update --startup-project ../BombasticIFC.API

# Rollback migration
dotnet ef database update PreviousMigrationName --startup-project ../BombasticIFC.API

# Generate script
dotnet ef migrations script --startup-project ../BombasticIFC.API --output migration.sql

# Build solution
dotnet build

# Run API
dotnet run --project BombasticIFC.API
```

---

**Document Version:** 1.0  
**Last Updated:** March 7, 2026  
**Maintained by:** BombasticIFC Development Team
