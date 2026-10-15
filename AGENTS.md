# SingBoxServer

## Коммиты

Формат: Conventional Commits без scope (без скобок), описание на русском языке.

```
<type>: <описание на русском>
```

Правила:
- `type` строчными: `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `build`, `ci`, `chore`, `revert`.
- После `type` идёт `:` и пробел. Scope в скобках не указывать.
- Описание на русском, с маленькой буквы, без точки в конце.
- Заголовок в одну строку, до ~72 символов.

Примеры:
```
feat: поддержка разных шаблонов
fix: форматирование конфигов
refactor: сброс соединений при смене сервера
```

## Сборка

```
dotnet build src/SingBoxServer/SingBoxServer.csproj
```
