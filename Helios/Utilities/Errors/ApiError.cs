using System.Text;
using System.Text.Json;
using Helios.Classes.Errors;
using Helios.Utilities.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Utilities.Errors;

public class ApiError
{
    public int StatusCode { get; private set; }
    public ErrorResponseBody Response { get; }
    
    public ApiError(string code, string message, string service, int numeric, int statusCode, params string[] messageVariables)
    {
        StatusCode = statusCode;
        Response = new ErrorResponseBody
        {
            ErrorCode = code,
            ErrorMessage = message,
            NumericErrorCode = numeric,
            OriginatingService = service,
            Intent = "Helios"
        };

        if (messageVariables.Length > 0)
            Response.MessageVars.AddRange(messageVariables);
    }

    public ApiError WithMessageVar(params string[] variables)
        {
            Response.MessageVars.Clear();
            Response.MessageVars.AddRange(variables);
            return this;
        }

        public ApiError WithIntent(Intents intent)
        {
            Response.Intent = intent.ToString();
            return this;
        }

        public ApiError WithMessage(string message)
        {
            Response.ErrorMessage = message;
            return this;
        }

        public ApiError ReplaceVariables()
        {
            var sb = new StringBuilder(Response.ErrorMessage);
            for (int i = 0; i < Response.MessageVars.Count; i++)
            {
                sb.Replace($"{{{i}}}", Response.MessageVars[i]);
            }
            Response.ErrorMessage = sb.ToString();
            return this;
        }

        public ApiError WithOriginatingService(string service)
        {
            Response.OriginatingService = service;
            return this;
        }

        public ApiError AddVariables(params string[] messageVariables)
        {
            Response.MessageVars.AddRange(messageVariables);
            return this;
        }

        public IActionResult Apply(HttpContext context)
        {
            context.Response.Headers.Append("Content-Type", "application/json");
            context.Response.Headers.Append("X-Epic-Error-Code", Response.NumericErrorCode.ToString());
            context.Response.Headers.Append("X-Epic-Error-Name", Response.ErrorCode);

            return new JsonResult(Response) { StatusCode = StatusCode };
        }
        
        public void ApplyToResponse(HttpContext context)
        {
            context.Response.StatusCode = StatusCode;
            context.Response.Headers.Append("X-Epic-Error-Code", Response.NumericErrorCode.ToString());
            context.Response.Headers.Append("X-Epic-Error-Name", Response.ErrorCode);
        
            var json = JsonSerializer.Serialize(Response);
            context.Response.WriteAsync(json);
        }

        public void Throw() => throw new ApiErrorException(this);

        public string GetFormattedMessage() => ReplaceVariables().Response.ErrorMessage;

        public string GetShortenedError() => $"{Response.ErrorCode} - {Response.ErrorMessage}";

        public void ThrowHttpException()
        {
            var json = JsonSerializer.Serialize(Response);
            throw new ApiException(StatusCode, json);
        }

        public ApiError WithDevMessage(string message, bool devMode)
        {
            if (devMode) Response.ErrorMessage += $" (Dev: {message})";
            return this;
        }
}