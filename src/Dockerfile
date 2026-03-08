# ── Backend Dockerfile (ASP.NET 10) ──
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Procument.slnx .
COPY Procument.Shared/Procument.Shared.csproj Procument.Shared/
COPY Procument.Data/Procument.Data.csproj Procument.Data/
COPY Procument.API/Procument.API.csproj Procument.API/
COPY Modules/Procument.Module.Catalog/Procument.Module.Catalog.csproj Modules/Procument.Module.Catalog/
COPY Modules/Procument.Module.Identity/Procument.Module.Identity.csproj Modules/Procument.Module.Identity/
COPY Modules/Procument.Module.RFQ/Procument.Module.RFQ.csproj Modules/Procument.Module.RFQ/
COPY Modules/Procument.Module.Sales/Procument.Module.Sales.csproj Modules/Procument.Module.Sales/
COPY Modules/Procument.Module.Purchasing/Procument.Module.Purchasing.csproj Modules/Procument.Module.Purchasing/

# Restore dependencies
RUN dotnet restore Procument.API/Procument.API.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish Procument.API/Procument.API.csproj -c Release -o /app/publish --no-restore

# ── Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000
ENTRYPOINT ["dotnet", "Procument.API.dll"]
