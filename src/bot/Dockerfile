FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Siphon.Bot/Siphon.Bot.csproj src/Siphon.Bot/
RUN dotnet restore src/Siphon.Bot/Siphon.Bot.csproj
COPY src/ src/
RUN dotnet publish src/Siphon.Bot/Siphon.Bot.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
VOLUME /data
ENV Db__Path=/data/bot.db
ENV ASPNETCORE_URLS=http://+:8090
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8090
ENTRYPOINT ["dotnet", "Siphon.Bot.dll"]
