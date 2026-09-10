# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/DoradoCloud.Gateway/ ./src/DoradoCloud.Gateway/

RUN dotnet restore src/DoradoCloud.Gateway/DoradoCloud.Gateway.csproj \
 && dotnet publish src/DoradoCloud.Gateway/DoradoCloud.Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DoradoCloud.Gateway.dll"]
