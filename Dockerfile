# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage. FROM --platform=$BUILDPLATFORM + `dotnet publish -a $TARGETARCH`
# cross-publishes to the target arch while Roslyn/crossgen run *natively* on
# the build machine — the one Dockerfile that stays fast on a native arm
# runner, on x64+QEMU, and (if ever needed) on the Pi itself. See
# docs/DEPLOYMENT.md for the full rationale.
#
# By the time this runs, CI has already staged the pass-and-play WASM app
# into src/DonitGames.Web/wwwroot/undercover/ (that folder is gitignored —
# it's a separate repo's `dotnet publish` output, not authored here) and run
# `dotnet test`. A plain `COPY . .` picks that bundle up along with
# everything else.
# ---------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY . .
RUN dotnet publish src/DonitGames.Web/DonitGames.Web.csproj \
    -c Release \
    -a $TARGETARCH \
    -p:PublishReadyToRun=true \
    -o /app/publish

# ---------------------------------------------------------------------------
# Runtime. Debian trixie-slim — not Alpine (musl + hand-adding icu-libs/tzdata
# erases the size win) and not -noble-chiseled (no shell means no curl
# healthcheck and no docker exec to debug). No trimming (PublishTrimmed
# breaks Razor component discovery at runtime) and no Native AOT.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The aspnet image ships neither curl nor wget; needed for HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# DataProtection keys must survive container restarts (CLAUDE.md: a fresh key
# ring invalidates every seat cookie). Create + chown ahead of time so a
# freshly mounted empty volume inherits the right ownership for $APP_UID.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app/.aspnet

ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Europe/Brussels \
    DOTNET_gcServer=0 \
    DOTNET_GCConserveMemory=5
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "DonitGames.Web.dll"]
