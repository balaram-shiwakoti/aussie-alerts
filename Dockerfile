FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /repo
COPY Directory.Build.props ./
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/Api src/Api
RUN dotnet publish src/Api/Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-runtime
WORKDIR /app
COPY --from=api-build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]

FROM node:24-alpine AS web-build
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ .
RUN npm run build

# Azure/production: ASP.NET serves the React build and API on one HTTPS origin.
FROM api-runtime AS production
COPY --from=web-build /web/dist ./wwwroot
