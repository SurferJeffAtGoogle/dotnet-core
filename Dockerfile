FROM microsoft/dotnet

COPY . webapp

RUN /bin/bash -c 'cd webapp; dotnet restore; dotnet build'

ENV ASPNETCORE_URLS="https://*:8080"

EXPOSE 8080

ENTRYPOINT ["/usr/bin/dotnet", "run", "-p", "webapp"]



