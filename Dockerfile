FROM node:24-bookworm-slim AS frontend
WORKDIR /build/Frontend
COPY src/CarRental.Web/Frontend/package.json src/CarRental.Web/Frontend/package-lock.json ./
RUN npm ci
COPY src/CarRental.Web/Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/CarRental.Web/CarRental.Web.csproj ./
RUN dotnet restore CarRental.Web.csproj
COPY src/CarRental.Web/ ./
COPY --from=frontend /build/wwwroot/frontend/ ./wwwroot/frontend/
RUN dotnet publish CarRental.Web.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ ./
RUN install -d -m 0700 -o app -g app /app/App_Data /app/App_Data/Reports /home/app/.aspnet/DataProtection-Keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CarRental.Web.dll"]
