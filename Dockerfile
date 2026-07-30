# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MLN122/MLN122.csproj", "MLN122/"]
RUN dotnet restore "MLN122/MLN122.csproj"

COPY . .
WORKDIR "/src/MLN122"
RUN dotnet build "MLN122.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MLN122.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "MLN122.dll"]
