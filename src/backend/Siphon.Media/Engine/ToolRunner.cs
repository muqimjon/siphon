using System.ComponentModel;
using CliWrap;
using Siphon.Media.Delivery;

namespace Siphon.Media.Engine;

internal static class ToolRunner
{
    public static async Task<CommandResult> RunGuardedAsync(this Command command, CancellationToken ct)
    {
        try
        {
            return await command.ExecuteAsync(ct);
        }
        catch (Win32Exception)
        {
            throw new MediaEngineException(ErrorCodes.Unavailable, "A required download tool is missing or broken on the server.");
        }
    }
}
