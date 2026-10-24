FROM node:22-alpine AS frontend
WORKDIR /source/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /source
COPY NuGet.Config Directory.Build.props ./
COPY src/Calendar.Api/Calendar.Api.csproj src/Calendar.Api/
RUN dotnet restore src/Calendar.Api/Calendar.Api.csproj --configfile NuGet.Config
COPY src/Calendar.Api/ src/Calendar.Api/
RUN dotnet publish src/Calendar.Api/Calendar.Api.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /publish ./
COPY --from=frontend /source/frontend/dist ./wwwroot
RUN mkdir /data && chown $APP_UID:$APP_UID /data
ENV PORT=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Calendar__DatabasePath=/data/calendar.db
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Calendar.Api.dll"]
