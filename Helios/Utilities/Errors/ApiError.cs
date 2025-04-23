using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Helios.Classes.Errors;
using Helios.Utilities.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Utilities.Errors;

public class ApiError
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };
    
    public int StatusCode { get; }
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
        {
            Response.MessageVars.Capacity = messageVariables.Length;
            Response.MessageVars.AddRange(messageVariables);
        }
    }

    public ApiError WithMessageVar(params string[] variables)
    {
        var messageVars = Response.MessageVars;
        messageVars.Clear();
        
        if (variables.Length > 0)
        {
            messageVars.Capacity = variables.Length; 
            messageVars.AddRange(variables);
        }
        
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
        if (Response.MessageVars.Count == 0)
            return this; 
            
        var messageVars = Response.MessageVars;
        var message = Response.ErrorMessage;
        
        if (messageVars.Count == 1)
        {
            Response.ErrorMessage = message.Replace("{0}", messageVars[0]);
            return this;
        }
        
        var sb = new StringBuilder(message.Length + 50);
        sb.Append(message);
        
        for (int i = 0; i < messageVars.Count; i++)
        {
            sb.Replace($"{{{i}}}", messageVars[i]);
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
        if (messageVariables.Length > 0)
        {
            var vars = Response.MessageVars;
            vars.Capacity = Math.Max(vars.Count + messageVariables.Length, vars.Capacity);
            vars.AddRange(messageVariables);
        }
        return this;
    }

    private string _numericErrorCodeString;
    private string NumericErrorCodeString => 
        _numericErrorCodeString ??= Response.NumericErrorCode.ToString();

    public IActionResult Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.Append("Content-Type", "application/json");
        headers.Append("X-Epic-Error-Code", NumericErrorCodeString);
        headers.Append("X-Epic-Error-Name", Response.ErrorCode);

        return new JsonResult(Response)
        {
            StatusCode = StatusCode,
            ContentType = "application/json",
            SerializerSettings = JsonOptions
        };
    }
    
    public async Task ApplyToResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCode;
        
        var headers = context.Response.Headers;
        headers.Append("Content-Type", "application/json");
        headers.Append("X-Epic-Error-Code", NumericErrorCodeString);
        headers.Append("X-Epic-Error-Name", Response.ErrorCode);
    
        await JsonSerializer.SerializeAsync(context.Response.Body, Response, JsonOptions);
    }

    public void ApplyToResponse(HttpContext context)
    {
        context.Response.StatusCode = StatusCode;
        
        var headers = context.Response.Headers;
        headers.Append("Content-Type", "application/json");
        headers.Append("X-Epic-Error-Code", NumericErrorCodeString);
        headers.Append("X-Epic-Error-Name", Response.ErrorCode);
    
        var json = JsonSerializer.Serialize(Response, JsonOptions);
        context.Response.WriteAsync(json);
    }

    public void Throw() => throw new ApiErrorException(this);

    public string GetFormattedMessage()
    {
        return Response.MessageVars.Count > 0 
            ? ReplaceVariables().Response.ErrorMessage 
            : Response.ErrorMessage;
    }

    public string GetShortenedError()
    {
        var errorCode = Response.ErrorCode;
        var errorMessage = Response.ErrorMessage;
        var totalLength = errorCode.Length + errorMessage.Length + 3; 

        var sb = new StringBuilder(totalLength);
        sb.Append(errorCode);
        sb.Append(" - ");
        sb.Append(errorMessage);
        return sb.ToString();
    }

    public void ThrowHttpException()
    {
        var json = JsonSerializer.Serialize(Response, JsonOptions);
        throw new ApiException(StatusCode, json);
    }

    public ApiError WithDevMessage(string message, bool devMode)
    {
        if (devMode)
        {
            var currentMsg = Response.ErrorMessage;
            var sb = new StringBuilder(currentMsg.Length + message.Length + 7);
            sb.Append(currentMsg);
            sb.Append(" (Dev: ");
            sb.Append(message);
            sb.Append(')');
            Response.ErrorMessage = sb.ToString();
        }
        return this;
    }
}