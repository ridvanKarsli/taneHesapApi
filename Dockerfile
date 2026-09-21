# taneHesap API — Railway (veya herhangi bir Docker ortamı) için üretim imajı.
# Railway bu dosyayı otomatik algılar; PORT ortam değişkenini Program.cs okur.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TaneHesap.slnx ./
COPY src/ src/
RUN dotnet publish src/TaneHesap.API/TaneHesap.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaneHesap.API.dll"]
