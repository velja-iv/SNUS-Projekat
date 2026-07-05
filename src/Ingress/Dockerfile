FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Ingress/Ingress.csproj src/Ingress/
RUN dotnet restore src/Ingress/Ingress.csproj

COPY . .
RUN dotnet publish src/Ingress/Ingress.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ingress.dll"]
