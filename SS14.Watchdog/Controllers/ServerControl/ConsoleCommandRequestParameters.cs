namespace SS14.Watchdog.Controllers.ServerControl;

public class ConsoleCommandRequestParameters : BasicRequestParameters
{
	public string Command { get; set; } = string.Empty;
}
