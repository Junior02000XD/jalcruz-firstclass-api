# ── Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar dependencias (capa cacheable).
COPY JalcruzFirstClass.Api.csproj ./
RUN dotnet restore JalcruzFirstClass.Api.csproj

# Compilar y publicar.
COPY . ./
RUN dotnet publish JalcruzFirstClass.Api.csproj -c Release -o /app --no-restore

# ── Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Railway inyecta la variable PORT en runtime; Kestrel debe escuchar ahí.
# Se expande con sh -c para que tome el PORT real (fallback 8080 en local).
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet JalcruzFirstClass.Api.dll"]
