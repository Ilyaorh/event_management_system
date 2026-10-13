14. Инструкция сборки и запуска
Требования
Для запуска проекта необходимы:
- .NET SDK 9;
- Git.

Получение проекта
git clone <адрес-репозитория>
cd EventManagement

Восстановление зависимостей
dotnet restore

Сборка
dotnet build

Выполнение тестов
dotnet test

Запуск
dotnet run --project src/EventManagement.Api