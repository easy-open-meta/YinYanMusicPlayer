# ============================================================================
# YinYanMusic API — Multi-stage .NET 10 build
#
# Build (on CI runner):
#   docker build --build-arg SoftwareVersion=26.9.16.1 -t <tag> -f Dockerfile .
#
# Run:
#   docker run -d --name YinYanMusic.Api --restart unless-stopped \
#     -p 127.0.0.1:5116:5116 \
#     -e ConnectionStrings__Default="Host=postgres;Port=5432;Database=yinyan_music;Username=postgres;Password=<PASS>" \
#     -e Jwt__SecretKey="<KEY>" \
#     -e Jwt__Issuer="YinYanMusic.Api" \
#     -e Jwt__Audience="YinYanMusic.App" \
#     -e Jwt__ExpiryMinutes=480 \
#     -e Media__BaseUrl="" \
#     -e Media__MusicDirectory="/music" \
#     -e Auth__RequireBootstrapCode=true \
#     -e ASPNETCORE_ENVIRONMENT=Production \
#     -e ASPNETCORE_URLS=http://0.0.0.0:5116 \
#     <tag>
# ============================================================================

# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# MUST be a valid NuGet/assembly version (e.g. 26.9.16.1).
# Passing something like "master-26.9.16.1" makes the SDK fail with
# NETSDK1018 (invalid NuGet version string) during publish.
ARG SoftwareVersion=dev

WORKDIR /src

# Copy solution + project files first (better layer cache for incremental builds)
COPY YinYanMusic.slnx ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/YinYanMusic.Core/YinYanMusic.Core.csproj src/YinYanMusic.Core/
COPY src/YinYanMusic.Data/YinYanMusic.Data.csproj     src/YinYanMusic.Data/
COPY src/YinYanMusic.Application/YinYanMusic.Application.csproj src/YinYanMusic.Application/
COPY src/YinYanMusic.Api/YinYanMusic.Api.csproj      src/YinYanMusic.Api/

# NOTE: restore MUST use the same -r/--self-contained as the publish below,
# otherwise project.assets.json has no 'net10.0/linux-x64' target and the
# publish step fails with NETSDK1047 (because it runs with --no-restore).
RUN dotnet restore src/YinYanMusic.Api/YinYanMusic.Api.csproj \
    -r linux-x64 \
    -p:SelfContained=true \
    --verbosity quiet

# Copy all sources
COPY src/ ./src/

# Publish (self-contained single file for easier deployment)
RUN dotnet publish src/YinYanMusic.Api/YinYanMusic.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:Version=${SoftwareVersion} \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG SoftwareVersion=dev

# Create non-root user (safer than root in container)
# ⚠️ uid/gid 固定为 1500：CI 用 bind mount 把宿主机的音乐/封面目录挂进来，
#    需要在宿主机上 chown 1500:1500 容器才能写入（封面抽取要写文件）。
RUN groupadd -g 1500 -r appgrp && useradd -r -u 1500 -g appgrp appuser

WORKDIR /app

# Media/music files directory (bind-mount from host or volume)
RUN mkdir -p /music && chown appuser:appgrp /music

# Copy published app
COPY --from=build /app/publish/ ./

# App runs as non-root
RUN chown -R appuser:appgrp /app

# PublishSingleFile 只产出可执行文件 YinYanMusic.Api，**没有** YinYanMusic.Api.dll，
# 所以必须给主程序补可执行位（COPY --from 的权限位在不同构建环境下不保证一致）。
RUN chmod +x /app/YinYanMusic.Api

# Defaults (can be overridden via -e flags)
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:5116 \
    ASPNETCORE_Logging__LogLevel__Default=Information \
    ASPNETCORE_Logging__LogLevel__Microsoft.AspNetCore=Warning \
    SoftwareVersion=${SoftwareVersion}

EXPOSE 5116

USER appuser

# 直接跑自包含单文件可执行程序。
# 别再写成 ["dotnet", "YinYanMusic.Api.dll"]：单文件发布里没有那个 dll，
# 容器会以 "The application 'YinYanMusic.Api.dll' does not exist" 退出，健康检查必挂。
ENTRYPOINT ["./YinYanMusic.Api"]
