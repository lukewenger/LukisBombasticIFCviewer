# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci --legacy-peer-deps

COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

# nginx.conf uses ${API_UPSTREAM} which is substituted at container start.
# Put it in /etc/nginx/templates/ so the entrypoint script can find it.
COPY nginx.conf /etc/nginx/templates/default.conf.template

# Custom entrypoint: runs envsubst on the template then starts nginx.
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Default upstream — works for Docker Compose / Coolify where the API
# service is named "api" and listens on 8080.
# Override at runtime for other environments (e.g. Kubernetes):
#   API_UPSTREAM=http://api-service.bombasticifccluster.svc.cluster.local
ENV API_UPSTREAM=http://api:8080

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:80/ || exit 1

ENTRYPOINT ["/docker-entrypoint.sh"]