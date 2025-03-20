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
                try
                {
                    await socket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
                    Console.WriteLine("Connected to the server.");

                    // Send the username to the server
                    await SendMessage(socket, $"USERNAME:{username}");

                    Console.WriteLine("Type 'create' to create a channel or 'join <invite-key>' to join one:");
                    string command = Console.ReadLine();

                    await SendMessage(socket, command);

                    // Start receiving messages in a separate task
                    var receivingTask = ReceiveMessages(socket);

                    while (socket.State == WebSocketState.Open)
                    {
                        string messageToSend = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(messageToSend))
                        {
                            await SendMessage(socket, messageToSend);
                        }
                    }

                    await receivingTask; // Await the receiving task to complete before exiting
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            Console.WriteLine("Connection closed.");
        }

        static async Task SendMessage(ClientWebSocket socket, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        static async Task ReceiveMessages(ClientWebSocket socket)
        {
            byte[] buffer = new byte[1024];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("Server closed the connection.");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", CancellationToken.None);
                    break;
                }
                else
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine(message);
                }
            }
        }
    }
}
