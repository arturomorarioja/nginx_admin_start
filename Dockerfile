FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY applications/ ./applications/
RUN dotnet publish applications/Learning.Web/Learning.Web.csproj -c Release -o /out/learning \
    && dotnet publish applications/Staff.Web/Staff.Web.csproj -c Release -o /out/staff \
    && dotnet publish applications/Certificates.Web/Certificates.Web.csproj -c Release -o /out/certificates

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx supervisor curl apache2-utils \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /etc/nginx/conf.d /etc/nginx/auth /var/lib/cedar /run/nginx
COPY --from=build /out/learning /opt/cedar/learning
COPY --from=build /out/staff /opt/cedar/staff
COPY --from=build /out/certificates /opt/cedar/certificates
COPY sites/ /srv/www/
COPY deploy/supervisord.conf /etc/supervisor/supervisord.conf
EXPOSE 80
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/supervisord.conf"]
