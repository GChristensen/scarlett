using System.Net.Http;
using System.Text;

namespace Scarlett.actions;

file class Args
{
    public string? URL;
    public string? Method;
    public Dictionary<string, string>? Headers;
    public string? Payload;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("url", out object? urlObj);
        args.TryGetValue("method", out object? methodObj);
        args.TryGetValue("headers", out object? headersObj);
        args.TryGetValue("payload", out object? payloadObj);

        URL = urlObj as string;
        Method = methodObj as string;
        Payload = methodObj as string;

        if (headersObj is Dictionary<string, object> headers)
        {
            Headers = new();
            
            foreach (var (header, value) in headers)
            {
                Headers[header] = (string)value;
            }
        }
    }
}

/// <summary>
/// Performs a HTTP request.
/// args:
///   - url: request URL - a string
///   - method: request method: "get" or "post"
///   - headers: request headers - a dictionary of string key/value pairs
///   - payload: request body - a string
/// </summary>
public class HTTPRequestAction: IAction
{
    public string Name => "http-request";
    
    public async Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return;

        var args = new Args(rawArgs);
        
        if (args.URL != null)
        {
            using (HttpClient client = new HttpClient())
            {
                if (args.Headers != null)
                {
                    foreach (var (header, value) in args.Headers)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header, (string)value);
                    }
                }
                
                try
                {
                    var method = args.Method ?? "get";
                    HttpResponseMessage? response = null;
                    
                    if (method.Equals("get", StringComparison.CurrentCultureIgnoreCase))
                    {
                        response = await client.GetAsync(args.URL);
                    }
                    else if (method.Equals("post", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var content = new StringContent(args.Payload ?? "", Encoding.UTF8);
                        response = await client.PostAsync(args.URL, content);
                    }

                    if (response != null)
                    {
                        response.EnsureSuccessStatusCode();

                        var responseBody = await response.Content.ReadAsStringAsync();
                        Log.Print("HTTP response: \n" + responseBody);
                    }

                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"HTTP Request error: {e.Message}");
                }
            }
        }
        else
        {
            Log.Print("HTTP request URL is not specified.");
        }
    }
}