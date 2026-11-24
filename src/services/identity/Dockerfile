ARG DOTNET_SDK_IMAGE
ARG DOTNET_ASPNET_IMAGE

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /repo/src/services/identity
COPY --from=packages . /repo/artifacts/packages
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY Identity.Api/Identity.Api.csproj Identity.Api/
RUN dotnet restore Identity.Api/Identity.Api.csproj -p:Configuration=Release
COPY Identity.Api/ Identity.Api/
RUN dotnet publish Identity.Api/Identity.Api.csproj -c Release --no-restore -o /app -p:MinVerSkip=true

FROM ${DOTNET_ASPNET_IMAGE} AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
