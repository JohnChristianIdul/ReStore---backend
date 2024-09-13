# Use the ASP.NET runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["ReStore - backend.csproj", "ReStore - Backend/"]
WORKDIR /src/ReStore - Backend
RUN dotnet restore "ReStore - backend.csproj"

# Copy the remaining files and build the application
COPY . .
RUN dotnet build "ReStore - backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application to a folder
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ReStore - backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage: use the base image and copy the published output
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy the credentials file into the container
COPY restore-db-98bee-8760dc2c521d.json /app/restore-db-98bee-8760dc2c521d.json

# Set the environment variable for Google Application Credentials
ENV GOOGLE_APPLICATION_CREDENTIALS="/app/restore-db-98bee-8760dc2c521d.json"

# Set the entry point for the container
ENTRYPOINT ["dotnet", "ReStore - backend.dll"]