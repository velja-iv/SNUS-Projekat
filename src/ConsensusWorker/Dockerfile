FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/ConsensusWorker/ConsensusWorker.csproj src/ConsensusWorker/
RUN dotnet restore src/ConsensusWorker/ConsensusWorker.csproj

COPY . .
RUN dotnet publish src/ConsensusWorker/ConsensusWorker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ConsensusWorker.dll"]
