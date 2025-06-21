using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

namespace L4FAspire;

public static class L4FAspireExtensions
{
    // Reuse a singleton JsonSerializerOptions to avoid the CA1869 performance warning
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Adds a custom dashboard command to open the given route.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithRoute(
        this IResourceBuilder<ProjectResource> builder,
        string customRoute)
    {
        CommandOptions options = new()
        {
            IconName = "Link",
            IconVariant = IconVariant.Filled
        };

        _ = builder.WithCommand(
            $"Route:{customRoute}",
            customRoute,
            context => OnLinkOpenerCommand(builder, context, route: customRoute),
            options);

        return builder;
    }

    /// <summary>
    /// Adds a custom dashboard command to open the given URL.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithCustomUrl(
        this IResourceBuilder<ProjectResource> builder,
        string customUrl)
    {
        CommandOptions options = new()
        {
            IconName = "Link",
            IconVariant = IconVariant.Filled
        };

        _ = builder.WithCommand(
            $"URL:{customUrl}",
            customUrl,
            context => OnLinkOpenerCommand(builder, context, customUrl: customUrl),
            options);

        return builder;
    }

    /// <summary>
    /// Always adds an OpenAPI JSON link.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithOpenApi(
        this IResourceBuilder<ProjectResource> builder)
    {
        CommandOptions options = new()
        {
            IconName = "Api",
            IconVariant = IconVariant.Filled
        };

        _ = builder.WithCommand(
            "OpenAPI JSON",
            "OpenAPI",
            context => OnLinkOpenerCommand(builder, context, route: "/swagger/v1/swagger.json"),
            options);

        return builder;
    }

    /// <summary>
    /// Always adds a Swagger UI link.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithSwaggerUi(
        this IResourceBuilder<ProjectResource> builder)
    {
        CommandOptions options = new()
        {
            IconName = "Code",
            IconVariant = IconVariant.Filled
        };

        _ = builder.WithCommand(
            "Swagger UI",
            "Swagger",
            context => OnLinkOpenerCommand(builder, context, route: "/swagger/index.html"),
            options);

        return builder;
    }

    /// <summary>
    /// Adds a Scalar link.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithScalar(
        this IResourceBuilder<ProjectResource> builder)
    {
        CommandOptions options = new()
        {
            IconName = "Chart",
            IconVariant = IconVariant.Filled
        };

        _ = builder.WithCommand(
            "Scalar Endpoint",
            "Scalar",
            context => OnLinkOpenerCommand(builder, context, route: "/Scalar/v1"),
            options);

        return builder;
    }

    // Single handler for opening URLs or routes
    private static Task<ExecuteCommandResult> OnLinkOpenerCommand(
        IResourceBuilder<ProjectResource> builder,
        ExecuteCommandContext context,
        string? route = null,
        string? customUrl = null,
        bool isHttps = false)
    {
        // Determine the URL: either the custom one or resolved via service discovery
        string url = customUrl
            ?? builder.GetEndpoint(isHttps ? "https" : "http").Url
            + (route?.StartsWith("/") == true ? route : $"/{route}");

        ProcessStartInfo psi = new(url)
        {
            UseShellExecute = true,
            Verb = "open"
        };
        _ = Process.Start(psi);

        return Task.FromResult(CommandResults.Success());
    }

    /// <summary>
    /// Registers a Blazor WASM project and writes the API-URL in wwwroot/appsettingsaspire.json.
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddWebAssemblyProject<TProject>(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ProjectResource> api)
        where TProject : IProjectMetadata, new()
    {
        IResourceBuilder<ProjectResource> project = builder.AddProject<TProject>(name);
        string projectPath = new TProject().ProjectPath;
        string wwwroot = Path.Combine(Path.GetDirectoryName(projectPath)!, "wwwroot");
        _ = Directory.CreateDirectory(wwwroot);

        string settingsFile = Path.Combine(wwwroot, "appsettingsaspire.json");
        if (!File.Exists(settingsFile))
        {
            File.WriteAllText(settingsFile, "{}");
        }

        _ = project.WithEnvironment(ctx =>
        {
            if (api.Resource.TryGetEndpoints(out IEnumerable<EndpointAnnotation>? endpoints) && endpoints.Any())
            {
                string uri = api.Resource.GetEndpoint("http").Url;
                string json = File.ReadAllText(settingsFile);
                Dictionary<string, object> dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, s_jsonOptions)
                           ?? [];

                dict["ApiUrlFromAspire"] = uri;
                File.WriteAllText(settingsFile, JsonSerializer.Serialize(dict, s_jsonOptions));

                ctx.EnvironmentVariables["ApiUrlFromAspire"] = uri;
            }
        });

        return project;
    }
}
