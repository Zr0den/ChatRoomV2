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
    private static List<WebSocket> channels = new();
    private static Dictionary<WebSocket, string> userNames = new();
    private static Dictionary<WebSocket, string> clientPublicKeys = new();
    public static int connectedClients = 0;

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

        try
        {
            // Receive public key and username
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var base64PublicKey = Encoding.UTF8.GetString(buffer, 0, result.Count);
            clientPublicKeys[socket] = base64PublicKey;

            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var username = Encoding.UTF8.GetString(buffer, 0, result.Count);
            userNames[socket] = username;
            connectedClients++;

            Console.WriteLine($"[New Connection] {username}. Current connections: {connectedClients}");

            if (connectedClients == 1)
            {
                channels = new List<WebSocket> { socket };
                Console.WriteLine($"[Channel Created] {username}");
            }
            else
            {
                channels.Add(socket);
                Console.WriteLine($"[Channel Joined] {username}");

                await ExchangePublicKeys(socket);
            }

            // Relay loop
            while (socket.State == WebSocketState.Open)
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var encryptedData = buffer[..result.Count];

                foreach (var client in channels)
                {
                    if (client != socket && client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(new ArraySegment<byte>(encryptedData), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex.Message}");
        }
        finally
        {
            if (socket.State != WebSocketState.Open)
            {
                channels.Remove(socket);
                userNames.Remove(socket);
                clientPublicKeys.Remove(socket);
                connectedClients--;
                Console.WriteLine($"[Disconnected] A user left. Clients left: {connectedClients}");
            }
        }
    }

    static async Task SendPlainMessage(WebSocket socket, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task ExchangePublicKeys(WebSocket newClient)
    {
        if (!clientPublicKeys.TryGetValue(newClient, out var newClientPublicKey))
            return;

        foreach (var existingClient in channels)
        {
            if (existingClient != newClient && existingClient.State == WebSocketState.Open)
            {
                // Send existing client's key to the new client
                if (clientPublicKeys.TryGetValue(existingClient, out var existingClientPublicKey))
                {
                    await SendPlainMessage(newClient, $"PK:{existingClientPublicKey}");
                }

                // Send new client's key to the existing client
                await SendPlainMessage(existingClient, $"PK:{newClientPublicKey}");
            }
        }
    }
}