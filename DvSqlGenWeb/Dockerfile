# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Устанавливаем рабочую директорию в контейнере
WORKDIR /app

# Копируем все файлы проекта в контейнер
COPY . ./

# Восстанавливаем зависимости
RUN dotnet restore

# Публикуем проект
RUN dotnet publish -c Release -o out

# Используем официальный образ .NET Runtime для выполнения
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем собранное приложение
COPY --from=build /app/out .

# Открываем порт
EXPOSE 5000

# Запускаем приложение
ENTRYPOINT ["dotnet", "DvSqlGenWeb.dll"]
