using CliWrap;
using CliWrap.Buffered;

async Task Start()
{
    Console.WriteLine("Starting setup...");
    var dir = GetPathInput();
    Console.WriteLine($"Using directory: {dir}");
    var domain = GetDomainInput();
    Console.WriteLine($"Using domain: {domain}");
    (var username, var password) = GetUsernameAndPassword();
    string? email;
    while (true)
    {
        Console.WriteLine("Enter cert resolver email: ");
        email = Console.ReadLine();
        if (email != null)
        {
            break;
        }
    }
    Console.WriteLine($"Using email: {email}");
    Console.WriteLine($"Using username: {username}");
    Console.WriteLine($"Using password: {password}");
    var hashedPassword = await GetHashedPassword(username, password);
    Console.WriteLine($"Using hashed password: {hashedPassword}");
    Console.WriteLine("Setting up directory...");
    SetupDirectory(dir);
    Console.WriteLine("Directory setup complete.");
    Console.WriteLine("Replacing text in files...");
    await ReplaceText(dir, hashedPassword, email, domain);
    Console.WriteLine("Text replacement complete.");
    Console.WriteLine("Setup complete!");
    Console.WriteLine("Start up the service with 'docker-compose up' Once you have confirmed it is working, you can run it detached with 'docker-compose up -d'");
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
    Console.WriteLine("Setting up dynamic directory. This is where the dynamic configuration files will be stored. An example one that ip whitelists has been created for you.");
    Directory.CreateDirectory($"{basePath}/core/data/dynamic");
    File.Copy("external/middlewares.yml", $"{basePath}/core/data/dynamic/middlewares.yml");
    File.Copy("external/docker-compose.yml", $"{basePath}/core/docker-compose.yml", true);
    File.Copy("external/traefik.yml", $"{basePath}/core/data/traefik.yml", true);
    File.Create($"{basePath}/core/data/acme.json");
    Cli.Wrap("chmod").WithArguments("600 " + $"{basePath}/core/data/acme.json").ExecuteBufferedAsync().Task.Wait();
}

async Task ReplaceText(string basePath, string hashedPassword, string email, string domain)
{
    var dockerComposePath = $"{basePath}/core/docker-compose.yml";
    var traefikPath = $"{basePath}/core/data/traefik.yml";
    var composeText = await File.ReadAllLinesAsync(dockerComposePath);
    ReplaceLineInTextFile(composeText, "traefik.http.middlewares.traefik-auth.basicauth.users", $"      - \"traefik.http.middlewares.traefik-auth.basicauth.users={hashedPassword}\"");
    await File.WriteAllLinesAsync(dockerComposePath, composeText);
    await File.WriteAllTextAsync(dockerComposePath, 
        (await File.ReadAllTextAsync(dockerComposePath)).Replace("example.com", domain));
    var traefikText = await File.ReadAllLinesAsync(traefikPath);
    ReplaceLineInTextFile(traefikText, "email", $"      email: {email}");
    await File.WriteAllLinesAsync(traefikPath, traefikText);
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

await Start();