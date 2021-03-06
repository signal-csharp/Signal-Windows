# Database

We use [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) with SQLite as our [database provider](https://docs.microsoft.com/en-us/ef/core/providers/sqlite/?tabs=dotnet-core-cli).

## Updating the Database Model

1. Add or remove properties in the model classes.
2. Open the Package Manager Console in Visual Studio
3. Ensure the default project in the Package Manager Console is selected as `Signal-Windows.Lib` as this is where the DbContext classes are.
4. Ensure the startup project is selected as `Signal-Windows` as this is the main application.
5. Run `Add-Migration <migration name> -Context <DbContext class>`. Typically `migration name` is just the number of the migration (m4, m5, m6, etc.). `DbContext class` is the class name of the DB we're migrating. Most migrations are to `SignalDBContext` but `LibsignalDBContext` is also valid.
6. Debug the app to ensure the migration runs successfully. Migrations are triggered [here](https://github.com/signal-csharp/Signal-Windows/blob/55598a6bdf57ce22f48fc18bc587b257122115a0/Signal-Windows/App.xaml.cs#L60-L61) as soon as the app is started up.
