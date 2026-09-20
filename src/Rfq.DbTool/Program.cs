return args.FirstOrDefault() switch
{
    null or "--help" or "-h" => ShowHelp(),
    "migrate" or "seed" or "reset-dev" => NotImplemented(args[0]),
    var command => UnknownCommand(command),
};

static int ShowHelp()
{
    Console.WriteLine("RFQ database tool");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project src/Rfq.DbTool -- <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  migrate    Apply pending database migrations (step 02)");
    Console.WriteLine("  seed       Seed development/demo data (step 02)");
    Console.WriteLine("  reset-dev  Reset, migrate, and seed a development database (step 02)");
    return 0;
}

static int NotImplemented(string command)
{
    Console.Error.WriteLine($"'{command}' is reserved for implementation step 02.");
    return 2;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Use --help to list available commands.");
    return 1;
}
