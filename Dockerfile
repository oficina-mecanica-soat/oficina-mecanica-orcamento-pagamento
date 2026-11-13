FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OficinaMecanica.OrcamentoPagamento.sln ./
COPY src/OficinaMecanica.OrcamentoPagamento.Domain/OficinaMecanica.OrcamentoPagamento.Domain.csproj src/OficinaMecanica.OrcamentoPagamento.Domain/
COPY src/OficinaMecanica.OrcamentoPagamento.Application/OficinaMecanica.OrcamentoPagamento.Application.csproj src/OficinaMecanica.OrcamentoPagamento.Application/
COPY src/OficinaMecanica.OrcamentoPagamento.Infrastructure/OficinaMecanica.OrcamentoPagamento.Infrastructure.csproj src/OficinaMecanica.OrcamentoPagamento.Infrastructure/
COPY src/OficinaMecanica.OrcamentoPagamento.API/OficinaMecanica.OrcamentoPagamento.API.csproj src/OficinaMecanica.OrcamentoPagamento.API/

RUN dotnet restore src/OficinaMecanica.OrcamentoPagamento.API/OficinaMecanica.OrcamentoPagamento.API.csproj

COPY src/ src/

RUN dotnet publish src/OficinaMecanica.OrcamentoPagamento.API/OficinaMecanica.OrcamentoPagamento.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "OficinaMecanica.OrcamentoPagamento.API.dll"]
