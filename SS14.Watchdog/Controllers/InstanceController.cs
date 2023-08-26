using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
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

		[HttpPost("dump")]
		public IActionResult Dump([FromHeader(Name = "Authorization")] string authorization, string key, [FromBody] DumpParameters parameters)
		{
			if (!TryAuthorize(authorization, key, out var failure, out var instance))
			{
				return failure;
			}

			instance.CreateDump(parameters);

			return Ok();
		}

		[HttpGet("dump/{fileName}")]
		public IActionResult GetDump([FromHeader(Name = "Authorization")] string authorization, string key, string fileName)
		{
			if (!TryAuthorize(authorization, key, out var failure, out var instance))
			{
				return failure;
			}

			string filePath = instance.GetDump(fileName);

			if (string.IsNullOrWhiteSpace(filePath))
			{
				return NotFound();
			}

			var mime = new FileExtensionContentTypeProvider();

			if (!mime.TryGetContentType(filePath, out var type))
			{
				type = "application/octet-stream";
			}

			return PhysicalFile(filePath, type);
		}

		[HttpGet("dumps")]
		public ActionResult<IEnumerable<string>> GetDumps([FromHeader(Name = "Authorization")] string authorization, string key)
		{
			if (!TryAuthorize(authorization, key, out var failure, out var instance))
			{
				return failure;
			}

			return Ok(instance.GetDumps());
		}

		[HttpPost("execute-command")]
		[ProducesResponseType(typeof(string), 200)]	
		public async Task<IActionResult> ExecuteCommand([FromHeader(Name = "Authorization")] string authorization, string key, [FromBody] ExecuteCommandParameters parameters, CancellationToken cancellationToken)
		{
			if (!TryAuthorize(authorization, key, out var failure, out var instance))
			{
				return failure;
			}

			var result = await instance.ExecuteCommand(parameters.Command, cancellationToken);
			return await GetContentResponse(result, cancellationToken);
		}

		private static async Task<ContentResult> GetContentResponse(HttpResponseMessage result, CancellationToken cancellationToken)
		{
			var content = await result.Content.ReadAsStringAsync(cancellationToken);

			return new ContentResult
			{
				StatusCode = (int)result.StatusCode,
				Content = result.StatusCode != System.Net.HttpStatusCode.OK ? content : JsonSerializer.Serialize(content),
				ContentType = "text/plain"
			};
		}

		[HttpGet("players-count")]
		[ProducesResponseType(typeof(string), 200)]
		public async Task<IActionResult> GetPlayersCount([FromHeader(Name = "Authorization")] string authorization, string key, CancellationToken cancellationToken)
		{
			if (!TryAuthorize(authorization, key, out var failure, out var instance))
			{
				return failure;
			}

			var result = await instance.GetPlayers(cancellationToken);

			return await GetContentResponse(result, cancellationToken);
		}

		private bool TryAuthorize(string authorization,
			string key,
			[NotNullWhen(false)] out ActionResult? failure,
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