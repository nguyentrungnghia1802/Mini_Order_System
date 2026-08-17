FROM node:24.15.0-alpine AS build
WORKDIR /workspace
COPY web/microshop-ui/package.json web/microshop-ui/package-lock.json ./
RUN npm ci
COPY web/microshop-ui/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY deploy/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/dist/microshop-ui/browser /usr/share/nginx/html
EXPOSE 8080
LABEL org.opencontainers.image.title="MicroShop Angular Web" \
      org.opencontainers.image.description="Angular SPA served through an unprivileged Nginx runtime" \
      org.opencontainers.image.version="phase6"
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget --quiet --output-document=- http://127.0.0.1:8080/health || exit 1

