FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Finalyst.API/Finalyst.API.csproj", "Finalyst.API/"]
RUN dotnet restore "Finalyst.API/Finalyst.API.csproj"
COPY . .
WORKDIR "/src/Finalyst.API"
RUN dotnet build "Finalyst.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Finalyst.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Finalyst.API.dll"]