using System.Diagnostics.CodeAnalysis;
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

            // TODO: we probably need constant-time comparisons for this?
            // Maybe?
            if (token != instance.ApiToken)
            {
                failure = Unauthorized();
                return false;
            }

            return true;
        }
    }
}
