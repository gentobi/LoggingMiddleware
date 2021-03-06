﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using NLog;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AspAPICore.Middlewares
{
    public class RequestLoggerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestLoggerMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // First, get the incoming request
            var request = await FormatRequest(context.Request);
            // Log the request to log file
            _logger.Info($"\n--------------------------- Received request from the client: {context.Connection.RemoteIpAddress} ------------------------------------------\n" +
                                $"-------------------------------------------------------------------------------------------------------------------------");
            _logger.Info(request);
            //
            // Copy a pointer to the original response body stream
            // Storage the original body stream to send it to client.
            var originalBodyStream = context.Response.Body;

            //Create a new memory stream...
            using (var responseBody = new MemoryStream())
            {
                // And use that for the temporary response body
                context.Response.Body = responseBody;

                // Continue down the Middleware pipeline, eventually returning to this class
                await _next(context);

                // Format the response from the server
                var response = await FormatResponse(context.Response);
                // Log the response to log file
                _logger.Info(response);
                _logger.Info($"\n----------------------------------------------- Request processed -------------------------------------------------------\n" +
                             $"-------------------------------------------------------------------------------------------------------------------------\n");
                // Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            var body = request.Body;

            // This will allows us to set the reader for the request back at the beginning of its stream.
            request.EnableRewind();

            // We now need to read the request stream.  First, we create a new byte[] with the same length as the request stream. Cause I can't use the read all method
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];

            // Then we copy the entire request stream into the new buffer.
            await request.Body.ReadAsync(buffer, 0, buffer.Length);

            // We convert the byte[] into a string using UTF8 encoding...
            var bodyAsText = Encoding.UTF8.GetString(buffer);

            // And finally, assign the read body back to the request body, which is allowed because of EnableRewind()
            request.Body = body;

            return $" REQUEST  - [{request.Method}] {request.Scheme}://{request.Host}{request.Path} {request.QueryString}" +
                   $"\n {(string.IsNullOrEmpty(bodyAsText) ? "" : "Request data: " + bodyAsText)}";
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            // We need to read the response stream from the beginning...
            response.Body.Seek(0, SeekOrigin.Begin);

            // And copy it into a string
            string text = await new StreamReader(response.Body).ReadToEndAsync();

            // We need to reset the reader for the response so that the client can read it.
            response.Body.Seek(0, SeekOrigin.Begin);

            // Return the string for the response, including the status code (200, 404, 401, etc.)
            return $" RESPONSE - Status code: {response.StatusCode} : Response data: {text}";
        }
    }
}
