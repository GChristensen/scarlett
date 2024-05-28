using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Scarlett.actions;

file class Args
{
    public string? MAC;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("mac", out object? macObj);

        MAC = macObj as string;
    }
}

/// <summary>
/// Sends magic packets to wake up a network host.
/// args:
///   - mac: the MAC address of the host to wake up
/// </summary>
public class WOLAction : IAction
{
    public string Name => "wol";
    
    public async Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return;

        var args = new Args(rawArgs);

        if (args.MAC != null)
        {
            try
            {
                Log.Print($"WOL: {args.MAC}");

                await WOL.WakeOnLan(args.MAC);
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        }
    }
}

public static class WOL
{
    public static async Task WakeOnLan(string macAddress)
    {
        byte[] magicPacket = BuildMagicPacket(macAddress);
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where((n) =>
            n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up))
        {
            IPInterfaceProperties iPInterfaceProperties = networkInterface.GetIPProperties();
            foreach (MulticastIPAddressInformation multicastIPAddressInformation in iPInterfaceProperties.MulticastAddresses)
            {
                IPAddress multicastIpAddress = multicastIPAddressInformation.Address;
                if (multicastIpAddress.ToString().StartsWith("ff02::1%", StringComparison.OrdinalIgnoreCase)) // Ipv6: All hosts on LAN (with zone index)
                {
                    UnicastIPAddressInformation? unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                        u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal).FirstOrDefault();
                    if (unicastIPAddressInformation != null)
                    {
                        await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                        break;
                    }
                }
                else if (multicastIpAddress.ToString().Equals("224.0.0.1")) // Ipv4: All hosts on LAN
                {
                    UnicastIPAddressInformation? unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                        u.Address.AddressFamily == AddressFamily.InterNetwork && !iPInterfaceProperties.GetIPv4Properties().IsAutomaticPrivateAddressingActive).FirstOrDefault();
                    if (unicastIPAddressInformation != null)
                    {
                        await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                        break;
                    }
                }
            }
        }
    }

    static byte[] BuildMagicPacket(string macAddress) // MacAddress in any standard HEX format
    {
        macAddress = Regex.Replace(macAddress, "[: -]", "");
        byte[] macBytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            macBytes[i] = Convert.ToByte(macAddress.Substring(i * 2, 2), 16);
        }

        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                for (int i = 0; i < 6; i++)  //First 6 times 0xff
                {
                    bw.Write((byte)0xff);
                }
                for (int i = 0; i < 16; i++) // then 16 times MacAddress
                {
                    bw.Write(macBytes);
                }
            }
            return ms.ToArray(); // 102 bytes magic packet
        }
    }

    static async Task SendWakeOnLan(IPAddress localIpAddress, IPAddress multicastIpAddress, byte[] magicPacket)
    {
        using (UdpClient client = new UdpClient(new IPEndPoint(localIpAddress, 0)))
        {
            for (int i = 0; i < 20; ++i)
            {
                await client.SendAsync(magicPacket, magicPacket.Length, multicastIpAddress.ToString(), 9);
                Thread.Sleep(10);
            }
        }
    }
}