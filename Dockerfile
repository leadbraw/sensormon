# Shared, parameterized Dockerfile that builds EITHER service.
# Pick which with `--build-arg PROJECT=SensorMon.Worker` (default) or
# `--build-arg PROJECT=SensorMon.Alerter`. Both reference SensorMon.Contracts.

# ---- Build stage ----
# Use the SDK image to restore + publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT=SensorMon.Worker
WORKDIR /src

# Copy csproj files first and restore — this layer caches unless dependencies change.
# The shared contracts project is needed for the project reference to resolve.
COPY src/SensorMon.Contracts/SensorMon.Contracts.csproj SensorMon.Contracts/
COPY src/${PROJECT}/${PROJECT}.csproj ${PROJECT}/
RUN dotnet restore ${PROJECT}/${PROJECT}.csproj

# Copy the rest and publish a release build.
COPY src/SensorMon.Contracts/ SensorMon.Contracts/
COPY src/${PROJECT}/ ${PROJECT}/
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
# The runtime image is much smaller than the SDK — no compiler shipped to prod.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
ARG PROJECT=SensorMon.Worker
# Bake the entry DLL name into the image so the (env-expanding) ENTRYPOINT can find it.
ENV APP_DLL=${PROJECT}.dll
WORKDIR /app

# Npgsql probes for the Kerberos/GSSAPI shared library during the auth handshake.
# The minimal runtime image doesn't ship it, which logs a noisy (but non-fatal)
# error on startup. Installing it makes the Postgres connection clean. (Harmless
# for the Alerter, which doesn't touch Postgres — kept here to share one image build.)
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Run as the non-root user the base image provides — good security hygiene.
USER $APP_UID

# `exec` replaces the shell with dotnet so the process is PID 1 and receives SIGTERM
# directly — that's what lets the BackgroundService cancel and shut down gracefully on
# pod termination instead of being SIGKILLed after the grace period.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet $APP_DLL"]
