using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Tasks;
using PnPPowerShell.MCPServer.Models;
using PnPPowerShell.MCPServer.Services;
using PnPPowerShell.MCPServer.Tools;

// Placed in the model's context by the host before the first tool call; kept under 1,500 characters.
const string ServerInstructions =
    "Legacy SharePoint PnP commands through SharePointPnPPowerShellOnline in Windows PowerShell 5.1. " +
    "Before composing any Connect-PnPOnline, run pnp_diagnose_connection with the site you are targeting, " +
    "follow its compatibility guidance, and do not assume modern PnP.PowerShell registration commands, " +
    "persisted-login files, or authentication switches work in this archived module. Confirm the target and " +
    "authentication method with the user before connecting.\n" +
    "After connecting, verify with pnp_get_connection_status and say what you are connected to. " +
    "pnp_get_best_practices holds the long form.";

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Holds the long-lived Windows PowerShell sessions the tools execute against, so a PnP connection survives
// across tool calls. Singleton: the sessions are the server's state, not a per-request concern.
builder.Services.AddSingleton<PowerShellSessionManager>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer(options => options.ServerInstructions = ServerInstructions)
    .WithStdioServerTransport()
    .WithTasks(
        new InMemoryMcpTaskStore(),
        options =>
        {
            // Long-running tenant operations are the reason Tasks is here: a client that supports the
            // extension can start the call and poll, instead of holding a request open. Metadata
            // lookups are quick and stay synchronous so short calls do not pay for a task round-trip.
            options.ExecutionModeSelector = request => request.Params?.Name switch
            {
                "pnp_run_command" => McpTaskExecutionMode.Optional,
                _ => McpTaskExecutionMode.Synchronous,
            };
        })
    // Registered with a source-generated resolver: tools returning typed structured content cannot be
    // serialized by reflection under native AOT.
    .WithTools<PnPPowerShellTools>(ToolJson.Options)
    .WithTools<ScriptSampleTools>(ToolJson.Options)
    .WithResources<PnPResources>();

await builder.Build().RunAsync();
