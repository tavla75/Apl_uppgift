# Citatboken

Responsiv citatsamling med Angular 20 och ett .NET 9 API. Användare kan skapa konto, logga in och hantera sina egna citat och böcker.

## Krav

- Node.js 20.19+ (eller en Angular 20-kompatibel nyare version)
- .NET 9 SDK

## Starta API:t

```powershell
dotnet run --project .\ConsoleApp1\ConsoleApp1.csproj --launch-profile http
```

API:t startar på `http://localhost:4200` och skapar SQLite-databasen `quotes.db`

## Starta Angular

```powershell
cd .\angular
npm install
npm start
```

Öppna `http://localhost:4200`. Angular-utvecklingsservern proxar `/api` till API:t. Skapa ett konto på registreringssidan för att börja använda citatsamlingen.

JWT-token lagras i webbläsarens local storage och skickas som Bearer-token. För produktion bör HTTPS användas och tokenhanteringen kompletteras med en genomgång av klientens XSS-risker.
