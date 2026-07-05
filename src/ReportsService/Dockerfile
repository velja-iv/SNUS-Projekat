FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/ReportsService/ReportsService.csproj src/ReportsService/
RUN dotnet restore src/ReportsService/ReportsService.csproj

COPY . .
RUN dotnet publish src/ReportsService/ReportsService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReportsService.dll"]
