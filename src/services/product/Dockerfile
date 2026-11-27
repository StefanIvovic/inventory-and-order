ARG DOTNET_SDK_IMAGE
ARG DOTNET_ASPNET_IMAGE

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /repo/src/services/product
COPY --from=packages . /repo/artifacts/packages
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY Product.Api/Product.Api.csproj Product.Api/
COPY Product.Contracts/Product.Contracts.csproj Product.Contracts/
RUN dotnet restore Product.Api/Product.Api.csproj -p:Configuration=Release
COPY Product.Contracts/ Product.Contracts/
COPY Product.Api/ Product.Api/
RUN dotnet publish Product.Api/Product.Api.csproj -c Release --no-restore -o /app -p:MinVerSkip=true

FROM ${DOTNET_ASPNET_IMAGE} AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Product.Api.dll"]
