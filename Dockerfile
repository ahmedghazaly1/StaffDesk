# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything so project references between StaffDesk.API, StaffDesk.Core,
# and StaffDesk.Infrastructure resolve correctly.
COPY . .

# Restore and publish only the API project (it will pull in Core/Infrastructure
# automatically via their project references).
RUN dotnet restore "StaffDesk.API/StaffDesk.API.csproj"
RUN dotnet publish "StaffDesk.API/StaffDesk.API.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects a PORT environment variable at runtime; ASP.NET Core needs to
# bind to it explicitly rather than its default port.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet StaffDesk.API.dll --urls http://0.0.0.0:${PORT:-8080}"]
