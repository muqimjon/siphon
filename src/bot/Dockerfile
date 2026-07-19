FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Siphon.Bot/Siphon.Bot.csproj src/Siphon.Bot/
RUN dotnet restore src/Siphon.Bot/Siphon.Bot.csproj
COPY src/ src/
RUN dotnet publish src/Siphon.Bot/Siphon.Bot.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
VOLUME /data
ENV Db__Path=/data/bot.db
ENTRYPOINT ["dotnet", "Siphon.Bot.dll"]
