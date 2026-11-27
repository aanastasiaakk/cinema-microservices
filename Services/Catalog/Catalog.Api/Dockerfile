# Catalog API — багатоетапна збірка. Контекст збірки — КОРІНЬ репозиторію
# (потрібні Directory.Build.props і сусідні проєкти):
#   docker build -f Services/Catalog/Catalog.Api/Dockerfile -t cinema/catalog-api .
#   docker run -p 8080:8080 -e ConnectionStrings__CinemaCatalog="Server=host.docker.internal,1433;..." cinema/catalog-api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# спочатку лише файли проєктів — шар restore кешується, доки не змінюються залежності
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Services/Catalog/Catalog.Domain/Catalog.Domain.csproj Services/Catalog/Catalog.Domain/
COPY Services/Catalog/Catalog.Dal/Catalog.Dal.csproj       Services/Catalog/Catalog.Dal/
COPY Services/Catalog/Catalog.Bll/Catalog.Bll.csproj       Services/Catalog/Catalog.Bll/
COPY Services/Catalog/Catalog.Api/Catalog.Api.csproj       Services/Catalog/Catalog.Api/
RUN dotnet restore Services/Catalog/Catalog.Api/Catalog.Api.csproj
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Services/Catalog/ Services/Catalog/
RUN dotnet publish Services/Catalog/Catalog.Api/Catalog.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
# не root-користувач з базового образу
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
