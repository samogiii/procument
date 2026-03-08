# ── Frontend Dockerfile (Nuxt 4) ──
FROM docker.arvancloud.ir/node:22-alpine AS build
WORKDIR /app

# Copy package files for layer caching
COPY package.json ./
RUN npm install

# Copy source and build
COPY . .

# Set API base for production (nginx will proxy /api to backend)
ENV NUXT_PUBLIC_API_BASE=/api

RUN npm run build

# ── Runtime ──
FROM docker.arvancloud.ir/node:22-alpine AS runtime
WORKDIR /app

COPY --from=build /app/.output .output

ENV HOST=0.0.0.0
ENV PORT=3000
ENV NODE_ENV=production

EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
