ARG NODE_IMAGE
ARG NGINX_IMAGE

FROM ${NODE_IMAGE} AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY angular.json tsconfig.json tsconfig.app.json ./
COPY public/ public/
COPY src/ src/
RUN npx ng build --configuration production

FROM ${NGINX_IMAGE} AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/storefront/browser /usr/share/nginx/html/shop
EXPOSE 8080
