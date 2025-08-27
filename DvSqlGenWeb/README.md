# DvSqlGenWeb (ASP.NET Core + RAG)

Минимальный Web API, который принимает вопрос и возвращает SQL:
- локальная LLM через Gpt4All Local Server (OpenAI-совместимый);
- ChromaDB для семантического поиска по вашей dv_schema.json;
- Ollama для эмбеддингов (`nomic-embed-text`).

## Запуск
1. Подними сервисы:
   - **Gpt4All Local Server** (порт/ключ см. `appsettings.json` → `Llm`).
   - **ChromaDB** (`http://localhost:8000` по умолчанию).
   - **Ollama** (`http://localhost:11434`) и скачай эмбеддер:
     ```
     ollama pull nomic-embed-text
     ```

2. Положи свой `dv_schema.json` в `App_Data/dv_schema.json`.

3. Собери и запусти:
dotnet build
dotnet run



---

## Что дальше можно добавить (по желанию)
- **Белый список таблиц** из контекста + «ремонт» SQL, если модель полезла в RefStaff.* (я показывал в прошлых сообщениях — легко вставить в `SqlRagService`).
- **Кеширование контекста** на время запроса.
- **Логи сырого ответа** (если захочешь дебажить до санитайзера).

Если хочешь, соберу тебе это в ZIP и приложу ссылку на скачивание — скажи только, нужно ли включить белый список и   