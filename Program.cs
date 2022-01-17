﻿using CliWrap;
using CliWrap.Buffered;

async void Start()
{
    Console.WriteLine("Starting setup...");
    var dir = GetPathInput();
    Console.WriteLine($"Using directory: {dir}");
    var domain = GetDomainInput();
    Console.WriteLine($"Using domain: {domain}");
    (var username, var password) = GetUsernameAndPassword();
    Console.WriteLine("Enter cert resolver email: ");
    var email = Console.ReadLine();
    Console.WriteLine($"Using email: {email}");
    Console.WriteLine($"Using username: {username}");
    Console.WriteLine($"Using password: {password}");
    var hashedPassword = await GetHashedPassword(username, password);
    Console.WriteLine($"Using hashed password: {hashedPassword}");
    Console.WriteLine("Setting up directory...");
    SetupDirectory(dir);
    Console.WriteLine("Directory setup complete.");
    Console.WriteLine("Replacing text in files...");
}

string GetPathInput()
{
    while (true)
    {
        Console.WriteLine("Enter full directory path to store files or leave blank to use current. A folder named docker will be created within that directory to store all config files.");
        var dirPath = Console.ReadLine();
        if (dirPath != null && dirPath.EndsWith("/"))
        {
            dirPath += "/";
        }
        try
        {
            if (Directory.Exists($"{dirPath}docker"))
            {
                Console.WriteLine("Docker directory found, deleting...");
                Directory.Delete($"{dirPath}docker", true);
            }
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                Console.WriteLine("Attempting to create docker directory in current directory...");
                dirPath = Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/docker").FullName;
                return dirPath;
            }
            Console.WriteLine($"Attempting to create docker directory in ${dirPath} ...");
            dirPath = Directory.CreateDirectory($"{dirPath}docker").FullName;
            return dirPath;
        } catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
        }
    }
}

// Get the domain name that we are going to use
string GetDomainInput()
{
    while (true)
    {
        Console.WriteLine("Enter domain name to use (e.g. example.com): ");
        var domain = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(domain)) return domain;
        Console.WriteLine("Domain name cannot be empty");
    }
}

(string username, string password) GetUsernameAndPassword()
{
    while (true)
    {
        Console.WriteLine("You will now be prompted to create a username and password for Traefik.");
        Console.Write("Press enter to continue...");
        Console.ReadLine();
        Console.WriteLine("Enter username: ");
        var username = Console.ReadLine();
        Console.WriteLine("Enter password: ");
        var password = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            return (username, password);
        }
        Console.WriteLine("Username or password cannot be empty");
    }
}

async Task<string> GetHashedPassword(string username, string password)
{
    BufferedCommandResult result;
    try
    {
        result = await Cli.Wrap("htpasswd")
            .WithArguments($"-nb {username} {password}")
            .ExecuteBufferedAsync();
    } catch (Exception e)
    {
        Console.WriteLine(e.Message);
        Console.Error.WriteLine("Some exception occured, this is probably because htpasswd is not installed. Please install htpasswd and try again.");
        throw;
    }
    var hash = result.StandardOutput.Trim();
    var output = hash.Replace("$", "$$");
    return output;
}

void SetupDirectory(string basePath)
{
    Console.WriteLine();
    Console.WriteLine("Creating core directory. This is where the traefik and portainer config files will be stored.");
    Directory.CreateDirectory($"{basePath}/core");
    Console.WriteLine("Creating apps directory. This is where the configurations or volumes for individual applications may be stored if you wish");
    Directory.CreateDirectory($"{basePath}/apps");
    Directory.CreateDirectory($"{basePath}/core/dynamic");
    File.Copy("external/docker-compose.yml", $"{basePath}/core/docker-compose.yml", true);
    File.Copy("external/traefik.yml", $"{basePath}/core/data/traefik.yml", true);
    File.Create($"{basePath}/core/data/acme.json");
    Cli.Wrap("chmod").WithArguments("600 " + $"{basePath}/core/data/acme.json").ExecuteBufferedAsync().Task.Wait();
}

async void ReplaceText(string basePath, string hashedPassword, string email, string domain)
{
    var dockerComposePath = $"{basePath}/core/docker-compose.yml";
    var composeText = await File.ReadAllLinesAsync(dockerComposePath);
    ReplaceLineInTextFile(composeText, "traefik.http.middlewares.traefik-auth.basicauth.users", $"      - \"traefik.http.middlewares.traefik-auth.basicauth.users={hashedPassword}\"");
    File.WriteAllLines(dockerComposePath, composeText);
    File.WriteAllText(dockerComposePath, File.ReadAllText(dockerComposePath).Replace("example.com", domain));
}

void ReplaceLineInTextFile(IList<string> fullText, string contains, string replaceWith, bool stopOnFirst=true)
{
    for (var i = 0; i < fullText.Count; i++)
    {
        if (!fullText[i].Contains(contains)) continue;
        fullText[i] = replaceWith;
        if (stopOnFirst) return;
    }
}

Start();