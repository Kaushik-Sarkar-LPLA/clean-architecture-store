# Multi-stage Dockerfile for .NET 9 Clean Architecture Web API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Web/Web.csproj src/Web/

RUN dotnet restore src/Web/Web.csproj

# Copy the entire source tree and publish
COPY . .
WORKDIR /src/src/Web
RUN dotnet publish Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CleanArchitecture.Web.dll"]
