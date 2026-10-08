FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/FraudWatch.Api/FraudWatch.Api.csproj src/FraudWatch.Api/
RUN dotnet restore src/FraudWatch.Api/FraudWatch.Api.csproj
COPY src/FraudWatch.Api/ src/FraudWatch.Api/
RUN dotnet publish src/FraudWatch.Api/FraudWatch.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FraudWatch.Api.dll"]
