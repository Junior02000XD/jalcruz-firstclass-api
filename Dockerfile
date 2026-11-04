# ── Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar dependencias (capa cacheable).
# linux-x64 explícito porque ReadyToRun (abajo) compila para una plataforma
# concreta, y restore y publish tienen que coincidir (los dos con el -p: si
# restore no lo lleva, no baja el compilador nativo y publish falla con
# NETSDK1094).
COPY JalcruzFirstClass.Api.csproj ./
RUN dotnet restore JalcruzFirstClass.Api.csproj -r linux-x64 -p:PublishReadyToRun=true

# Compilar y publicar con ReadyToRun: el IL sale precompilado a código nativo y
# el arranque casi no pasa por el JIT. Importa porque la API está en Serverless
# en Railway y arranca en frío cada vez que despierta. La imagen crece algo.
COPY . ./
RUN dotnet publish JalcruzFirstClass.Api.csproj -c Release -o /app --no-restore \
    -r linux-x64 --self-contained false -p:PublishReadyToRun=true

# ── Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Le pide al GC que compacte más a menudo para devolver memoria (0-9; 5 es
# un punto medio). Con la CPU ociosa, el costo es nada y la RAM baja.
ENV DOTNET_GCConserveMemory=5

# Railway inyecta la variable PORT en runtime; Kestrel debe escuchar ahí.
# Se expande con sh -c para que tome el PORT real (fallback 8080 en local).
#
# El "$@" pasa a la API los argumentos que se agreguen al contenedor. Existe por
# el pre-deploy de railway.json (`--migrar`): la doc de Railway no aclara si ese
# comando REEMPLAZA el ENTRYPOINT o se le AGREGA como argumentos. Así funciona
# de las dos formas: si lo reemplaza, corre tal cual; si se agrega, "--migrar"
# llega igual a args. Sin el "$@", sh -c tiraría los argumentos en silencio y el
# pre-deploy levantaría el servidor entero y colgaría el despliegue.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet JalcruzFirstClass.Api.dll \"$@\"", "sh"]
