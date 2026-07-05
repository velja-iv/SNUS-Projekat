FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/SensorClient/SensorClient.csproj src/SensorClient/
RUN dotnet restore src/SensorClient/SensorClient.csproj

COPY . .
RUN dotnet publish src/SensorClient/SensorClient.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SensorClient.dll"]
