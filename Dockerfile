FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ConflictDuplicationDetector.sln ./
COPY src/ConflictDuplicationDetector.Core/ src/ConflictDuplicationDetector.Core/
COPY src/ConflictDuplicationDetector.Api/ src/ConflictDuplicationDetector.Api/

RUN dotnet restore src/ConflictDuplicationDetector.Api/ConflictDuplicationDetector.Api.csproj
RUN dotnet publish src/ConflictDuplicationDetector.Api/ConflictDuplicationDetector.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV VectorStore__PersistPath=/data/vectors.json
ENV Storage__UploadsPath=/data/uploads

RUN mkdir -p /data/uploads

COPY --from=build /app/publish .

EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "ConflictDuplicationDetector.Api.dll"]
