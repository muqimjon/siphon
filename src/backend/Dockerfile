FROM node:22-alpine AS web
WORKDIR /web
COPY src/web/package*.json src/web/
RUN cd src/web && npm ci
COPY src/web/ src/web/
COPY src/shared/ src/shared/
RUN cd src/web && npx ng build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/backend/Siphon.Api/Siphon.Api.csproj src/backend/Siphon.Api/
COPY src/backend/Siphon.Media/Siphon.Media.csproj src/backend/Siphon.Media/
COPY src/backend/Siphon.Accounts/Siphon.Accounts.csproj src/backend/Siphon.Accounts/
RUN dotnet restore src/backend/Siphon.Api/Siphon.Api.csproj
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/Siphon.Api/Siphon.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# ffmpeg from apt, yt-dlp and gallery-dl from pip so they work on both amd64 and arm64
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg python3 python3-pip ca-certificates \
    && pip3 install --no-cache-dir --break-system-packages yt-dlp gallery-dl \
    && ln -sf "$(which yt-dlp)" /usr/local/bin/yt-dlp \
    && ln -sf "$(which gallery-dl)" /usr/local/bin/gallery-dl \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
COPY --from=web /web/src/web/dist/siphon-web ./wwwroot

VOLUME /data
ENV Siphon__TempRoot=/data/tmp
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Siphon.Api.dll"]
