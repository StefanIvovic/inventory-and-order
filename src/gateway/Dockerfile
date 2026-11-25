ARG DOTNET_SDK_IMAGE
ARG DOTNET_ASPNET_IMAGE

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY Gateway.Api/Gateway.Api.csproj Gateway.Api/
RUN dotnet restore Gateway.Api/Gateway.Api.csproj -p:Configuration=Release
COPY Gateway.Api/ Gateway.Api/
RUN dotnet publish Gateway.Api/Gateway.Api.csproj -c Release --no-restore -o /app

FROM ${DOTNET_ASPNET_IMAGE} AS final
WORKDIR /app
COPY --from=build /app .
RUN mkdir /keys && chown $APP_UID /keys
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
