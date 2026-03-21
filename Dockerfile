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
COPY test/BombasticIFC.Tests/BombasticIFC.Tests.csproj ./test/BombasticIFC.Tests/

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

# Install xeokit-convert — converts IFC → XKT directly via web-ifc (no IfcConvert needed)
# Binary name changed from convert2xkt to xeokit-convert in @xeokit/xeokit-convert@1.3.0+
RUN apt-get update && apt-get install -y --no-install-recommends \
    nodejs npm ca-certificates && \
    rm -rf /var/lib/apt/lists/*

RUN npm install -g @xeokit/xeokit-convert@1.3.1

ENTRYPOINT ["dotnet", "BombasticIFC.API.dll"]
