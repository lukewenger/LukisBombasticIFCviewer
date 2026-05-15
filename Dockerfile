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

# Node build stage — install and rebuild xeokit-convert with native addons.
# Uses node:20-bookworm-slim; the final stage installs Node 20 from NodeSource
# so the compiled .node binaries are ABI-compatible (both Node 20, ABI 115).
# build-essential + python3 + make are required by node-gyp to compile native
# addons (e.g. @loaders.gl/polyfills btoa.node). They stay in this stage only.
FROM node:20-bookworm-slim AS node-builder
RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential python3 make && \
    rm -rf /var/lib/apt/lists/*
# Copy patch script before npm install so it is available in this stage.
# @loaders.gl/polyfills ships a native btoa.node addon that is not always compiled
# for every platform/Node version. Node.js >=16 provides atob/btoa as globals,
# so we patch dist/index.js to remove the missing native import after npm install.
COPY docker/patch-btoa.js /tmp/patch-btoa.js
RUN npm install -g @xeokit/xeokit-convert@1.3.1 && \
    cd /usr/local/lib/node_modules/@xeokit/xeokit-convert && \
    npm rebuild && \
    node /tmp/patch-btoa.js && \
    # npm may symlink /usr/local/bin/xeokit-convert → convert2xkt.js.
    # Redirecting with > follows the symlink and overwrites the JS source file.
    # Remove the symlink first so > creates a new standalone wrapper file instead.
    # The wrapper invokes node with the full package path so Node.js walks up to
    # the package.json ("type":"module") and parses the entry point as ESM.
    rm -f /usr/local/bin/xeokit-convert && \
    printf '#!/bin/sh\nexec node "/usr/local/lib/node_modules/@xeokit/xeokit-convert/convert2xkt.js" "$@"\n' \
        > /usr/local/bin/xeokit-convert && \
    chmod +x /usr/local/bin/xeokit-convert

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create storage directory
RUN mkdir -p /data/storage /app/seed-data

COPY --from=publish /app/publish .
COPY src/BombasticIFC.API/data/storage/samples/Duplex.xkt /app/seed-data/Duplex.xkt

# Railway injects PORT; Kubernetes leaves PORT unset (probes target 8080).
# EXPOSE here is documentation only — actual port is controlled by --urls at runtime.
EXPOSE 8080

# Install Node.js 20 from NodeSource so the runtime ABI matches the node:20-bookworm-slim
# builder stage above.  Debian bookworm's default apt repo ships Node 18, which has a
# different V8 ABI (108 vs 115) and cannot load the .node binaries compiled by Node 20.
# curl is needed only to fetch the NodeSource setup script and is removed afterwards.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    apt-get purge -y curl && apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

# Copy the pre-built xeokit-convert package and its CLI entry point
COPY --from=node-builder /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=node-builder /usr/local/bin/xeokit-convert /usr/local/bin/xeokit-convert

# Use shell form so ${PORT:-8080} is expanded at container start:
#   Railway  → sets PORT to its assigned value → Kestrel binds there
#   Kubernetes → PORT not set → falls back to 8080 (matches probe port)
#   Docker Compose → sets PORT=80 in environment → Kestrel binds to 80
# exec replaces the shell process so .NET receives SIGTERM directly.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet BombasticIFC.API.dll --urls http://+:${PORT:-8080}"]
