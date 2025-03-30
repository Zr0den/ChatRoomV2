using Cryptography;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketConsoleClient
{
    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("Enter your username:");
            string username = Console.ReadLine();

            using (ClientWebSocket socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
                Console.WriteLine("Connected to the server.");

                await SendMessage(socket, username);

                Console.WriteLine("Type 'create' to create a channel or 'join <invite-key>' to join one:");
                string command = Console.ReadLine();
                await SendMessage(socket, command);

                var receivingTask = ReceiveMessages(socket);

                while (socket.State == WebSocketState.Open)
                {
                    string messageToSend = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(messageToSend))
                    {
                        await SendMessage(socket, messageToSend);
                    }
                }

                await receivingTask;
            }

            Console.WriteLine("Connection closed.");
        }

        static async Task SendMessage(ClientWebSocket socket, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("[Error] Cannot send an empty message.");
                    return;
                }

                byte[] encryptedMessage = AesHelper.Encrypt(message);
                //Console.WriteLine($"1: {message} -- 2: {encryptedMessage} -- 3: {AesHelper.Decrypt(encryptedMessage)}");
                Console.WriteLine($"--- Sending encrypted message: {Encoding.Default.GetString(encryptedMessage)} --- ");
                await socket.SendAsync(new ArraySegment<byte>(encryptedMessage), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to send message: {ex.Message}");
            }
        }

        static async Task ReceiveMessages(ClientWebSocket socket)
        {
            byte[] buffer = new byte[1024];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Console.WriteLine(AesHelper.Decrypt(buffer[..result.Count]));
            }
        }
    }
}
