using System.IO.Ports;

namespace Scarlett.actions;

file class Args
{
    public string? Port;
    public int? Baud;
    public bool? DTR;
    public string? Presend;
    public string? Send;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("port", out object? portNameObj);
        args.TryGetValue("baud", out object? baudObj);
        args.TryGetValue("dtr", out object? dtrObj);
        args.TryGetValue("presend", out object? presendObj);
        args.TryGetValue("send", out object? sendObj);

        Port = portNameObj as string;
        Baud = (int?)(long?)baudObj;
        DTR = (bool?)dtrObj;
        Presend = presendObj as string;
        Send = sendObj as string;
    }
}

/// <summary>
/// Sends a serial command to a connected device, e.g. Arduino.
/// args:
///   - port: serial port name e.g. "COM3"
///   - baud: integer port baud rate, 9600 is the default, optional
///   - dtr: send data terminal ready signal, boolean, the default is true, optional
///   - presend: string to send before sending the main payload, not sent if omitted, optional
///   - send: string to send through the port
/// </summary>
public class SerialAction: IAction
{
    public string Name => "serial";

    private SerialPort CreatePort(string portName, int baudRate, bool dtrEnable)
    {
        Parity parity = Parity.None;
        int dataBits = 8;
        StopBits stopBits = StopBits.One;
        
        SerialPort serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
        serialPort.DtrEnable = dtrEnable;
        serialPort.ReadTimeout = 2000;
        serialPort.WriteTimeout = 2000;
        
        return serialPort;
    }
    
    public Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);

        if (args.Port != null && args.Send != null) 
        {
            SerialPort? serialPort = null;

            try
            {
                int baudRate = args.Baud ?? 9600;
                bool dtrEnable = args.DTR ?? true; 
                serialPort = CreatePort(args.Port, baudRate, dtrEnable);
                serialPort.DtrEnable = false;
                serialPort.Open();

                if (serialPort.IsOpen)
                {
                    if (args.Presend != null)
                    {
                        char[] characterToPresend = args.Presend.ToCharArray();
                        serialPort.Write(characterToPresend, 0, characterToPresend.Length);
                        Log.Print($"Preliminary string '{args.Presend}' written to the serial port.");
                        Thread.Sleep(100);
                    }
                    
                    char[] characterToSend = args.Send.ToCharArray();
                    serialPort.Write(characterToSend, 0, characterToSend.Length);
                    Log.Print($"Command '{args.Send}' written to the serial port.");

                    serialPort.Close();
                }
                else
                {
                    Log.Print("Unable to open the serial port.");
                }
            }
            catch (Exception ex)
            {
                Log.Print($"Serial error: {ex.Message}");
            }
            finally
            {
                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();
            }
        }
        else
        {
            Log.Print("Serial port or payload not specified.");
        }
        
        return Task.CompletedTask;
    }

}