using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LiveReloadRepro
{
	public class RequestLoggingMiddleware
	{
		private readonly ILogger<RequestLoggingMiddleware> _Log;
		private readonly RequestDelegate _Next;

		public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger, RequestDelegate next)
		{
			_Log = logger ?? throw new ArgumentNullException(nameof(logger));
			_Next = next ?? throw new ArgumentNullException(nameof(next));
		}

		public async Task InvokeAsync(HttpContext context)
		{
			Stopwatch Stopwatch = Stopwatch.StartNew();

			HttpRequest Request = context.Request;
			HttpResponse Response = context.Response;

			Stream OriginalResponseBody = Response.Body;
			MemoryStream RedirectedResponseBody;
			Response.Body = RedirectedResponseBody = new MemoryStream();

			try
			{
				await _Next(context).ConfigureAwait(false);
			}
			finally
			{
				_Log.LogDebug("{Response}", await FlushResponseAndReadBodyAsString(Response.ContentType, OriginalResponseBody, RedirectedResponseBody).ConfigureAwait(false));

				Response.Body = OriginalResponseBody;
				RedirectedResponseBody.Dispose();
			}
		}

		private static async Task<string> FlushResponseAndReadBodyAsString(string contentType, Stream originalResponseBody, Stream redirectedResponseBody)
		{
			redirectedResponseBody.Position = 0;
			await redirectedResponseBody.CopyToAsync(originalResponseBody).ConfigureAwait(false);

			string MediaType = contentType?.Split(';')[0];

			if (MediaType == "application/json" || MediaType == "application/xml" || MediaType == "application/soap+xml" || MediaType == "text/plain")
			{
				redirectedResponseBody.Position = 0;
				using StreamReader reader = new StreamReader(redirectedResponseBody);
				string Body = await reader.ReadToEndAsync().ConfigureAwait(false);
				redirectedResponseBody.Position = 0;
				return Body;
			}

			return null;
		}
	}
}
