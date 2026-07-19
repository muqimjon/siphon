FROM node:22-slim AS web
WORKDIR /build
COPY src/shared ./src/shared
COPY src/web/package.json src/web/package-lock.json ./src/web/
WORKDIR /build/src/web
RUN npm ci
WORKDIR /build
COPY src/web ./src/web
WORKDIR /build/src/web
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=web /build/src/web/dist ./src/web/dist
RUN dotnet publish src/backend/Siphon.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg python3 python3-pip curl \
 && pip3 install --break-system-packages gallery-dl \
 && mkdir -p /opt/siphon/bin \
 && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o /opt/siphon/bin/yt-dlp \
 && chmod +x /opt/siphon/bin/yt-dlp \
 && apt-get purge -y curl && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN useradd -m siphon && chown -R siphon /opt/siphon && mkdir -p /tmp/siphon && chown siphon /tmp/siphon
USER siphon
ENV ASPNETCORE_URLS=http://+:8080 \
    Siphon__Tools__FfmpegPath=/usr/bin/ffmpeg \
    Siphon__Tools__YtDlpPath=/opt/siphon/bin/yt-dlp \
    Siphon__Tools__GalleryDlPath=/usr/local/bin/gallery-dl
EXPOSE 8080
ENTRYPOINT ["dotnet", "Siphon.Api.dll"]
