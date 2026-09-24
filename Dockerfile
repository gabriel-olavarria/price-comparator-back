# ==============================
# BUILD
# ==============================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copiar primero los proyectos para aprovechar la caché de Docker
COPY ["PriceComparator.Api/PriceComparator.Api.csproj", "PriceComparator.Api/"]
COPY ["PriceComparator.Application/PriceComparator.Application.csproj", "PriceComparator.Application/"]
COPY ["PriceComparator.Domain/PriceComparator.Domain.csproj", "PriceComparator.Domain/"]
COPY ["PriceComparator.Infrastructure/PriceComparator.Infrastructure.csproj", "PriceComparator.Infrastructure/"]

# Restaurar dependencias
RUN dotnet restore "PriceComparator.Api/PriceComparator.Api.csproj"

# Copiar todo el código
COPY . .

# Publicar la API
WORKDIR "/src/PriceComparator.Api"

RUN dotnet publish "PriceComparator.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


# ==============================
# RUNTIME
# ==============================
FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Directorio donde se guardarán los snapshots
ENV Snapshots__RootPath=Snapshots/Data

EXPOSE 8080

ENTRYPOINT ["dotnet", "PriceComparator.Api.dll"]