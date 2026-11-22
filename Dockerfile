FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

#установка зависимостей (.NET 8 SDK)
RUN apt-get update && apt-get install -y dotnet-sdk-8.0 && rm -rf /var/lib/apt/lists/*

#копирование папки с исходным кодом
WORKDIR /build
COPY src/ ./src/

# компиляция приложения
RUN dotnet publish src/TerrariaZoomPatcher.Linux/TerrariaZoomPatcher.Linux.csproj -c Release -o /app/publish

#переход в папку с собранным приложением
WORKDIR /app/publish

#объявление тома для внешних данных (папки с Terraria.exe)
VOLUME ["/app/game"]

# запуск приложения, передавая ему папку тома
ENTRYPOINT ["dotnet", "TerrariaUltrawidePatcher.dll", "/app/game"]
