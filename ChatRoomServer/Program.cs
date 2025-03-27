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

class Program
{
    private static Dictionary<string, List<WebSocket>> channels = new();
    private static Dictionary<string, string> inviteKeys = new();
    private static Dictionary<WebSocket, string> userNames = new();
    private static Random random = new();

    static async Task Main()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Server started at ws://localhost:5000/");

        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
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
        byte[] buffer = new byte[1024];

        try
        {
            // Receive username
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.Count == 0)
            {
                Console.WriteLine("[Error] Empty message received.");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Empty message", CancellationToken.None);
                return;
            }

            byte[] encryptedData = buffer[..result.Count];

            string decryptedUsername;
            try
            {
                decryptedUsername = AesHelper.Decrypt(encryptedData);
                Console.WriteLine($"[Decrypted Message] {decryptedUsername}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to decrypt message: {ex.Message}");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Decryption error", CancellationToken.None);
                return;
            }

            userNames[socket] = decryptedUsername;
            Console.WriteLine($"[New Connection] Username assigned: {decryptedUsername}");

            // Receive command (create or join)
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.Count == 0)
            {
                Console.WriteLine("[Error] Empty command received.");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Empty command", CancellationToken.None);
                return;
            }

            string command;
            try
            {
                command = AesHelper.Decrypt(buffer[..result.Count]);
                string channelId = "";

                if (command.StartsWith("create"))
                {
                    channelId = Guid.NewGuid().ToString();
                    string inviteKey = GenerateInviteKey();
                    channels[channelId] = new List<WebSocket> { socket };
                    inviteKeys[inviteKey] = channelId;
                    await SendMessage(socket, $"\n[System] Channel created! Invite key: {inviteKey}\n");
                }
                else if (command.StartsWith("join"))
                {
                    string[] parts = command.Split(' ');
                    if (parts.Length == 2 && inviteKeys.TryGetValue(parts[1], out channelId) && channels.ContainsKey(channelId))
                    {
                        channels[channelId].Add(socket);
                        await SendMessage(socket, "\n[System] Joined channel! You can now chat.\n");
                    }
                    else
                    {
                        await SendMessage(socket, "[Error] Invalid invite key.");
                        return;
                    }
                }
                else
                {
                    await SendMessage(socket, "[Error] Invalid command.");
                    return;
                }

                while (socket.State == WebSocketState.Open && channelId != null)
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        byte[] encryptedMessage = buffer[..result.Count]; 
                        string decryptedMessage = string.Empty;
                        try
                        {
                            decryptedMessage = AesHelper.Decrypt(encryptedMessage); 
                            Console.WriteLine($"[Decrypted] {decryptedMessage}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Error] Failed to decrypt message: {ex.Message}");
                            continue; 
                        }

                        string username = userNames[socket]; 
                        string messageWithUsername = $"{username}: {decryptedMessage}";

                        Console.WriteLine($"[Channel {channelId}] {messageWithUsername}");

                        foreach (var client in channels[channelId])
                        {
                            if (client != socket && client.State == WebSocketState.Open)
                            {
                                await SendMessage(client, messageWithUsername); 
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to decrypt command: {ex.Message}");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Decryption error", CancellationToken.None);
                return;
            }

            Console.WriteLine($"[Received Command] {command}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Fatal Error] {ex.Message}");
            await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Server error", CancellationToken.None);
        }
    }

    static async Task SendMessage(WebSocket socket, string message)
    {
        byte[] encryptedMessage = AesHelper.Encrypt(message);
        await socket.SendAsync(new ArraySegment<byte>(encryptedMessage), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    static string GenerateInviteKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] key = new char[6];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = chars[random.Next(chars.Length)];
        }
        return new string(key);
    }
}