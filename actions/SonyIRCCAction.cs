using System.Text;
using System.Net.Http;
using static System.Net.HttpStatusCode;

namespace Scarlett.actions;

file class Args
{
    public string? Host;
    public string? IRCC;

    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("host", out object? hostObj);
        args.TryGetValue("cmd", out object? cmdObj);

        Host = hostObj as string;
        IRCC = cmdObj as string;
    }
}

/// <summary>
/// Sends an IRCC command to an IP-controlled Sony TV.
/// args:
///   - host: the ip address of the connected TV
///   - cmd: IRCC command
/// </summary>
public class SonyIRCCAction: IAction
{
    public string Name => "sony-ircc";
    
    private SonyController _sonyController;
    
    public SonyIRCCAction()
    {
        _sonyController = new SonyController();
    }
    
    public async Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return;

        var args = new Args(rawArgs);

        if (args.Host != null && args.IRCC != null)
        {
            Log.Print($"Sony IRCC: {args.IRCC}");

            await _sonyController.SendIRCode(args.Host, args.IRCC);
        }
    }
}

public class SonyController
{
    private class RetryException : Exception {}
    
    private static readonly HttpClient Сlient = new HttpClient();
    private const int RETRY_TIMEOUT = 16000; 

    static SonyController()
    {
        Сlient.Timeout = TimeSpan.FromSeconds(6);
    }

    public SonyController()
    {
    }

    private async Task SendPayload(string host, string irCode, bool retrying = false)
    {
        try
        {
            var payload =
                $"<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                $"s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:X_SendIRCC " +
                $"xmlns:u=\"urn:schemas-sony-com:service:IRCC:1\"><IRCCCode>{irCode}</IRCCCode" +
                $"></u:X_SendIRCC></s:Body></s:Envelope>";

            var httpContent = new StringContent(payload, Encoding.UTF8, "text/xml");

            httpContent.Headers.Add("SOAPACTION", "urn:schemas-sony-com:service:IRCC:1#X_SendIRCC");
            httpContent.Headers.Add("X-Auth-PSK", "1230");

            var response = await Сlient.PostAsync($"http://{host}/sony/IRCC?", httpContent);

            var responseString = await response.Content.ReadAsStringAsync();

            Log.Print(responseString);

            if (response.StatusCode == NotFound)
            {
                if (!retrying)
                    throw new RetryException();
            }
        }
        catch (Exception e)
        {
            Log.Print(e);

            if (!retrying)
            {
                throw new RetryException();
            }
        }
    } 

    public async Task SendIRCode(string host, string irCode)
    {
        try
        {
            await SendPayload(host, irCode);
        }
        catch (RetryException)
        {
            try
            {
                Thread.Sleep(RETRY_TIMEOUT);
                await SendPayload(host, irCode, true);
            }
            catch (Exception e2)
            {
                Log.Print(e2);
            }
        }
    }
}