FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/IngestionService/IngestionService.csproj src/IngestionService/
RUN dotnet restore src/IngestionService/IngestionService.csproj

COPY . .
RUN dotnet publish src/IngestionService/IngestionService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IngestionService.dll"]
