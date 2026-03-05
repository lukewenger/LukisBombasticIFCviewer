# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY BombasticIFCcluster.sln ./
COPY src/BombasticIFC.Domain/BombasticIFC.Domain.csproj ./src/BombasticIFC.Domain/
COPY src/BombasticIFC.Application/BombasticIFC.Application.csproj ./src/BombasticIFC.Application/
COPY src/BombasticIFC.Infrastructure/BombasticIFC.Infrastructure.csproj ./src/BombasticIFC.Infrastructure/
COPY src/BombasticIFC.API/BombasticIFC.API.csproj ./src/BombasticIFC.API/
COPY src/BombasticIFC.Shared/BombasticIFC.Shared.csproj ./src/BombasticIFC.Shared/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY src/ ./src/

# Build
WORKDIR /src/src/BombasticIFC.API
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create storage directory
RUN mkdir -p /data/storage /app/seed-data

COPY --from=publish /app/publish .
COPY src/BombasticIFC.API/data/storage/samples/Duplex.xkt /app/seed-data/Duplex.xkt

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "BombasticIFC.API.dll"]
