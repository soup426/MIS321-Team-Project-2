FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["MulhollandRealEstate.API.csproj", "./"]
RUN dotnet restore "./MulhollandRealEstate.API.csproj"

COPY . .
RUN dotnet publish "MulhollandRealEstate.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

EXPOSE 3002
ENV ASPNETCORE_URLS=http://+:3002

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MulhollandRealEstate.API.dll"]
