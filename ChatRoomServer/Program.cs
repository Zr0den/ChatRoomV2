using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Cryptography;
using System.Threading.Channels;
using Newtonsoft.Json;

class Program
{
    private static Dictionary<string, List<WebSocket>> channels = new();
    private static Dictionary<string, string> inviteKeys = new();
    private static Dictionary<WebSocket, string> userNames = new();
    private static Dictionary<WebSocket, string> clientPublicKeys = new();
    private static Random random = new();

    static async Task Main()
    {
        HttpListener listener = new();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Server started at ws://localhost:5000/");

        while (true)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _ = HandleClient(wsContext.WebSocket);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    static async Task HandleClient(WebSocket socket)
    {
        byte[] buffer = new byte[2048];
        string? channelId = null;

        try
        {
            // Receive public key and username
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var base64PublicKey = Encoding.UTF8.GetString(buffer, 0, result.Count);
            clientPublicKeys[socket] = base64PublicKey;

            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var username = Encoding.UTF8.GetString(buffer, 0, result.Count);
            userNames[socket] = username;
            Console.WriteLine($"[New Connection] {username}");

            // Receive command (create/join)
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var command = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (command.StartsWith("create"))
            {
                channelId = Guid.NewGuid().ToString();
                var inviteKey = GenerateInviteKey();
                channels[channelId] = new List<WebSocket> { socket };
                inviteKeys[inviteKey] = channelId;
                await SendPlainMessage(socket, $"[System] Channel created! Invite key: {inviteKey}");
            }
            else if (command.StartsWith("join"))
            {
                var parts = command.Split(' ');
                if (parts.Length == 2 && inviteKeys.TryGetValue(parts[1], out channelId))
                {
                    channels[channelId].Add(socket);
                    await SendPlainMessage(socket, "[System] Joined channel!");
                }
                else
                {
                    await SendPlainMessage(socket, "[Error] Invalid invite key");
                    return;
                }
            }

            // Relay loop
            while (socket.State == WebSocketState.Open)
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var encryptedData = buffer[..result.Count];

                foreach (var client in channels[channelId!])
                {
                    if (client != socket && client.State == WebSocketState.Open)
                    {
                        //var buffer = new byte[1024];  // Adjust buffer size as needed
                        //var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        //var data = buffer[..result.Count];

                        // Extract the encrypted key length (first 4 bytes)
                        int keyLen = BitConverter.ToInt32(encryptedData, 0);

                        // Extract the encrypted key (next `keyLen` bytes)
                        byte[] encryptedKey = encryptedData[4..(4 + keyLen)];

                        // Extract the encrypted message (remaining bytes after the encrypted key)
                        byte[] encryptedMessage = encryptedData[(4 + keyLen)..];

                        // Relay the encrypted data to the recipient client
                        await client.SendAsync(new ArraySegment<byte>(encryptedData), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex.Message}");
        }
    }

    static async Task SendPlainMessage(WebSocket socket, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    static string GenerateInviteKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}