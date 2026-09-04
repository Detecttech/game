# ==============================================================================
# QuizBattle - High-Speed Production Docker Image
# ==============================================================================
FROM node:20-bookworm-slim AS runner

# Install C++ build tools required by native better-sqlite3 bindings
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    make \
    g++ \
    curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy shared assets (character configs, etc.)
COPY shared ./shared

WORKDIR /app/server

# Install production dependencies only (compiles better-sqlite3 for Linux)
COPY server/package*.json ./
RUN npm install --omit=dev && npm cache clean --force

# Copy pre-compiled server and web portal
COPY server/dist ./dist
COPY server/web-portal/dist ./web-portal/dist

# Copy Unity WebGL build
COPY game-client/webgl-build /app/game-client/webgl-build

# Prepare persistent database storage directory
RUN mkdir -p /app/server/data

# Environment configuration
ENV NODE_ENV=production
ENV MODE=wan
ENV PORT=8080
ENV DB_PATH=/app/server/data/quizbattle.db

# Cloud Run defaults to 8080
EXPOSE 8080

# Persistent volume for SQLite data when running on Compute Engine or Docker
VOLUME ["/app/server/data"]

CMD ["node", "dist/index.js"]
