FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ReplayFilesViewApi.csproj", "./"]
RUN dotnet restore "ReplayFilesViewApi.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "./ReplayFilesViewApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ReplayFilesViewApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:PublishAot=false

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y sudo docker.io && \
    rm -rf /var/lib/apt/lists/*
RUN adduser --disabled-password --gecos "" appuser && \
    echo "appuser ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/appuser && \
    chmod 0440 /etc/sudoers.d/appuser
COPY --from=publish /app/publish .
RUN chown -R appuser:appuser /app
USER appuser
ENTRYPOINT ["dotnet", "ReplayFilesViewApi.dll"]
