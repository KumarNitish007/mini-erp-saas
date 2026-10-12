# Build context: repository root (needs Directory.Build.props and /database).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props MiniErp.sln ./
COPY src/backend/MiniErp.Domain/MiniErp.Domain.csproj src/backend/MiniErp.Domain/
COPY src/backend/MiniErp.Application/MiniErp.Application.csproj src/backend/MiniErp.Application/
COPY src/backend/MiniErp.Infrastructure/MiniErp.Infrastructure.csproj src/backend/MiniErp.Infrastructure/
COPY src/backend/MiniErp.Api/MiniErp.Api.csproj src/backend/MiniErp.Api/
RUN dotnet restore src/backend/MiniErp.Api/MiniErp.Api.csproj

COPY database/ database/
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/MiniErp.Api/MiniErp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Kolkata
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MiniErp.Api.dll"]
