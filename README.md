# Sign Language Learning System

ASP.NET Core Razor Pages application with MySQL.

## Local setup

Install the .NET SDK required by the project file and MySQL. Create the database using database/schema.sql. Copy appsettings.example.json to appsettings.Development.json and set your local database credentials. Set BootstrapAdmin credentials only when you want to create an administrator. Run with dotnet run --environment Development. Keep local settings out of Git.

Database backups, user uploads, original archives, reports, and build output are intentionally excluded to protect private data. Supply your own lesson and quiz content.
