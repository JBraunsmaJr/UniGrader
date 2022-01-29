FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["UniGrader.Dashboard/UniGrader.Dashboard.csproj", "UniGrader.Dashboard/"]
RUN dotnet restore "UniGrader.Dashboard/UniGrader.Dashboard.csproj"
COPY . .
WORKDIR "/src/UniGrader.Dashboard"
RUN dotnet build "UniGrader.Dashboard.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UniGrader.Dashboard.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UniGrader.Dashboard.dll"]
