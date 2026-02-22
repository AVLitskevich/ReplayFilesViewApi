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
RUN dotnet publish "./ReplayFilesViewApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=publish /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "ReplayFilesViewApi.dll"]
