# ---- Build stage ----
# Use the SDK image to restore + publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first and restore — this layer caches unless dependencies change.
COPY src/SensorMon.Worker/SensorMon.Worker.csproj SensorMon.Worker/
RUN dotnet restore SensorMon.Worker/SensorMon.Worker.csproj

# Copy the rest and publish a trimmed, release build.
COPY src/SensorMon.Worker/ SensorMon.Worker/
RUN dotnet publish SensorMon.Worker/SensorMon.Worker.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
# The runtime image is much smaller than the SDK — no compiler shipped to prod.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Npgsql probes for the Kerberos/GSSAPI shared library during the auth handshake.
# The minimal runtime image doesn't ship it, which logs a noisy (but non-fatal)
# error on startup. Installing it makes the Postgres connection clean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Run as the non-root user the base image provides — good security hygiene
# and something interviewers like to hear.
USER $APP_UID

ENTRYPOINT ["dotnet", "SensorMon.Worker.dll"]
