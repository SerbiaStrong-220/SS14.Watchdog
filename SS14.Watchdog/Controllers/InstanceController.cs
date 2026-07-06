using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS14.Watchdog.Components.ServerManagement;
using SS14.Watchdog.Utility;

namespace SS14.Watchdog.Controllers
{
    [Route("/instances/{key}")]
    [Controller]
    public class InstanceController : ControllerBase
    {
        private readonly IServerManager _serverManager;

        public InstanceController(IServerManager serverManager)
        {
            _serverManager = serverManager;
        }

        [HttpPost("restart")]
        public async Task<IActionResult> Restart([FromHeader(Name = "Authorization")] string authorization, string key)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            await instance.DoRestartCommandAsync();
            return Ok();
        }

        [HttpPost("stop")]
        public async Task<IActionResult> Stop([FromHeader(Name = "Authorization")] string authorization, string key)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            await instance.DoStopCommandAsync(new ServerInstanceStopCommand());
            return Ok();
        }

        [HttpPost("update")]
        public IActionResult Update([FromHeader(Name = "Authorization")] string authorization, string key)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            instance.HandleUpdateCheck();
            return Ok();
        }

        [HttpPost("command")]
        public async Task<IActionResult> Command(
            [FromHeader(Name = "Authorization")] string authorization,
            [FromHeader(Name = "X-Command-Token")] string? commandToken,
            string key,
            [FromBody] ConsoleCommandRequest? request)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            if (!TryAuthorizeCommand(commandToken, instance, out failure))
            {
                return failure;
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return BadRequest("Command must not be empty.");
            }

            if (request.Command.Contains('\n') || request.Command.Contains('\r'))
            {
                return BadRequest("Command must be a single line.");
            }

            try
            {
                await instance.DoConsoleCommandAsync(request.Command, HttpContext.RequestAborted);
                return Ok();
            }
            catch (NotSupportedException e)
            {
                return Conflict(e.Message);
            }
            catch (IOException)
            {
                return Conflict("Failed to write command to the server process.");
            }
            catch (ObjectDisposedException)
            {
                return Conflict("Failed to write command to the server process.");
            }
            catch (InvalidOperationException e)
            {
                return Conflict(e.Message);
            }
        }

        [HttpGet("status")]
        public async Task<ActionResult<string?>> ServerStatus([FromHeader(Name = "Authorization")] string authorization, string key)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return (ActionResult)failure;
            }

            return await instance.GetServerStatusAsync();
        }

        [HttpGet("replays")]
        public IActionResult GetReplays([FromHeader(Name = "Authorization")] string authorization, string key)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            return Ok(instance.GetReplays());
        }

        [HttpGet("replays/{fileName}")]
        public IActionResult GetReplay([FromHeader(Name = "Authorization")] string authorization, string key, string fileName)
        {
            if (!TryAuthorize(authorization, key, out var failure, out var instance))
            {
                return failure;
            }

            var replay = instance.GetReplay(fileName);

            if (replay is null)
            {
                return NotFound();
            }

            return File(replay, "application/octet-stream");
        }

        [NonAction]
        public bool TryAuthorize(string authorization,
            string key,
            [NotNullWhen(false)] out IActionResult? failure,
            [NotNullWhen(true)] out IServerInstance? instance)
        {
            instance = null;

            if (string.IsNullOrEmpty(authorization))
            {
                failure = Unauthorized();
                return false;
            }

            if (!AuthorizationUtility.TryParseBasicAuthentication(authorization, out failure, out var authKey,
                out var token))
            {
                return false;
            }

            if (authKey != key)
            {
                failure = Forbid();
                return false;
            }

            if (!_serverManager.TryGetInstance(key, out instance))
            {
                failure = NotFound();
                return false;
            }

            if (string.IsNullOrEmpty(instance.ApiToken) || !FixedTimeEquals(token, instance.ApiToken))
            {
                failure = Unauthorized();
                return false;
            }

            return true;
        }

        [NonAction]
        public bool TryAuthorizeCommand(
            string? commandToken,
            IServerInstance instance,
            [NotNullWhen(false)] out IActionResult? failure)
        {
            if (string.IsNullOrEmpty(instance.CommandToken))
            {
                failure = Conflict("Command token is not configured.");
                return false;
            }

            if (string.IsNullOrEmpty(commandToken) || !FixedTimeEquals(commandToken, instance.CommandToken))
            {
                failure = Unauthorized();
                return false;
            }

            failure = null;
            return true;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);

            return leftBytes.Length == rightBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        public sealed class ConsoleCommandRequest
        {
            public string Command { get; set; } = default!;
        }
    }
}
