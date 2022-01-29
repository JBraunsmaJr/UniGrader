FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

RUN apt update
RUN apt install git
RUN curl -fsSLO https://get.docker.com/builds/Linux/x86_64/docker-17.04.0-ce.tgz \
  && tar xzvf docker-17.04.0-ce.tgz \
  && mv docker/docker /usr/local/bin \
  && rm -r docker docker-17.04.0-ce.tgz

RUN curl -L "https://github.com/docker/compose/releases/download/1.29.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
RUN chmod +x /usr/local/bin/docker-compose

# install powershell
COPY install-powershell.sh install-powershell.sh
RUN ./install-powershell.sh
RUN rm -f install-powershell.sh

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["UniGrader/UniGrader.csproj", "UniGrader/"]
RUN dotnet restore "UniGrader/UniGrader.csproj"
COPY . .
WORKDIR "/src/UniGrader"
RUN dotnet build "UniGrader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UniGrader.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UniGrader.dll"]
