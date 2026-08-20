# Linux container. Poink.Sim.csproj swaps in TorchSharp-cpu (instead of the win-x64-only
# TorchSharp-cuda-windows used for local dev) whenever $(OS) isn't Windows_NT, and
# NativeMemory.GetPhysicalMemory() no-ops off Windows instead of crashing on the missing
# kernel32 export -- both keyed off the build host OS, so local Windows dev is unaffected.
#
# Build context is the repo root (Poink.Sim references sibling projects), e.g.:
#   docker build -f Poink.Sim/Dockerfile -t poink-sim .
# or via Poink.Sim/docker-compose.yml, which sets this up for you.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Poink.Sim/Poink.Sim.csproj", "Poink.Sim/"]
COPY ["Poink.Shared.Client/Poink.Shared.Client.csproj", "Poink.Shared.Client/"]
COPY ["Poink.CTrader/Poink.CTrader.csproj", "Poink.CTrader/"]
RUN dotnet restore "Poink.Sim/Poink.Sim.csproj"
COPY . .
WORKDIR /src/Poink.Sim
# SkipTypeScriptCompile bypasses Poink.Shared.Client's node/tsc build step (see that
# project's .csproj) -- not installed in this image, and unneeded since its compiled
# output is already checked into wwwroot/js.
RUN dotnet build "Poink.Sim.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:SkipTypeScriptCompile=true

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Poink.Sim.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:SkipTypeScriptCompile=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Poink.Sim.dll"]
