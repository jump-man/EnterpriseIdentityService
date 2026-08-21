FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS build

ARG SOURCE_REVISION=unknown

WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/dotnet-tools.json

RUN dotnet tool restore

COPY src/EnterpriseIdentityService.Api/EnterpriseIdentityService.Api.csproj src/EnterpriseIdentityService.Api/
COPY src/EnterpriseIdentityService.Application/EnterpriseIdentityService.Application.csproj src/EnterpriseIdentityService.Application/
COPY src/EnterpriseIdentityService.Contracts/EnterpriseIdentityService.Contracts.csproj src/EnterpriseIdentityService.Contracts/
COPY src/EnterpriseIdentityService.Domain/EnterpriseIdentityService.Domain.csproj src/EnterpriseIdentityService.Domain/
COPY src/EnterpriseIdentityService.Infrastructure/EnterpriseIdentityService.Infrastructure.csproj src/EnterpriseIdentityService.Infrastructure/

RUN dotnet restore src/EnterpriseIdentityService.Api/EnterpriseIdentityService.Api.csproj

COPY src/ src/

RUN dotnet publish src/EnterpriseIdentityService.Api/EnterpriseIdentityService.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

RUN dotnet ef migrations bundle \
    --project src/EnterpriseIdentityService.Infrastructure/EnterpriseIdentityService.Infrastructure.csproj \
    --startup-project src/EnterpriseIdentityService.Api/EnterpriseIdentityService.Api.csproj \
    --configuration Release \
    --no-build \
    --target-runtime linux-x64 \
    --output /app/migrations/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble AS final

ARG SOURCE_REVISION=unknown

LABEL org.opencontainers.image.title="Enterprise Identity Service" \
      org.opencontainers.image.description="Enterprise Identity Service API and explicit EF Core migration bundle" \
      org.opencontainers.image.revision="$SOURCE_REVISION"

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --chown=app:app --from=build /app/publish/ ./
COPY --chown=app:app --from=build /app/migrations/efbundle /app/migrations/efbundle

USER app

ENTRYPOINT ["dotnet", "EnterpriseIdentityService.Api.dll"]
