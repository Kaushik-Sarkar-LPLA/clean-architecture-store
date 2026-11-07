# Multi-stage Dockerfile for .NET 10 Clean Architecture Web API + Angular Client
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install Node.js (v20) and npm for Angular SpaRoot build
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    npm install -g @angular/cli && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy project files for caching restore layer
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Web/Web.csproj src/Web/

RUN dotnet restore src/Web/Web.csproj

# Copy the entire source tree and publish (includes npm install & ng build)
COPY . .
WORKDIR /src/src/Web
RUN dotnet publish Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=PostgreSQL

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CleanArchitecture.Web.dll"]
