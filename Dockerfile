FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Rezerv.BookingEngine.Domain/Rezerv.BookingEngine.Domain.csproj src/Rezerv.BookingEngine.Domain/
COPY src/Rezerv.BookingEngine.Application/Rezerv.BookingEngine.Application.csproj src/Rezerv.BookingEngine.Application/
COPY src/Rezerv.BookingEngine.Infrastructure/Rezerv.BookingEngine.Infrastructure.csproj src/Rezerv.BookingEngine.Infrastructure/
COPY src/Rezerv.BookingEngine.Api/Rezerv.BookingEngine.Api.csproj src/Rezerv.BookingEngine.Api/
RUN dotnet restore src/Rezerv.BookingEngine.Api/Rezerv.BookingEngine.Api.csproj

COPY src/ src/
RUN dotnet publish src/Rezerv.BookingEngine.Api/Rezerv.BookingEngine.Api.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rezerv.BookingEngine.Api.dll"]

