# API Gateway (YARP). Контекст збірки — корінь репозиторію:
#   docker build -f Platform.ApiGateway/Dockerfile -t cinema/gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.ApiGateway/Platform.ApiGateway.csproj           Platform.ApiGateway/
RUN dotnet restore Platform.ApiGateway/Platform.ApiGateway.csproj
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.ApiGateway/      Platform.ApiGateway/
RUN dotnet publish Platform.ApiGateway/Platform.ApiGateway.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]
