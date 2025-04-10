using Cryptography;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketConsoleClient
{

    class Program
    {
        public static RSAHelper rsa = new RSAHelper();
        public static string recipientPublicKey = "TODO";

        static async Task Main()
        {
            Console.Write("Enter your username: ");
            var username = Console.ReadLine();

            // Generate RSA keys and public key
            var publicKey = rsa.PublicKey;

            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
            Console.WriteLine("Connected to server.");

            // Send public key and username to server
            await SendRaw(socket, publicKey);
            await SendRaw(socket, username);

            // Wait for server response and handle command input
            Console.WriteLine("Type 'create' or 'join <inviteKey>':");
            var command = Console.ReadLine();
            await SendRaw(socket, command);

            // Receive messages in the background
            _ = Task.Run(() => ReceiveMessages(socket));

            while ((socket.State == WebSocketState.Open))
            {
                var message = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(message)) continue;

                // Encrypt message with AES
                using var aes = Aes.Create();
                var (encryptedMessage, iv) = AesHelper.Encrypt(message, aes.Key);

                // Encrypt AES key and IV with recipient's RSA public key
                byte[] aesKeyAndIv = aes.Key.Concat(aes.IV).ToArray();
                var encryptedKey = RSAHelper.Encrypt(aesKeyAndIv, recipientPublicKey);  // Encrypted AES key

                // Create the packet to send (encrypted key length + encrypted key + encrypted message)
                var packet = new List<byte>();
                packet.AddRange(BitConverter.GetBytes(encryptedKey.Length));  // 4-byte length of encrypted key
                packet.AddRange(encryptedKey);  // Encrypted AES key
                packet.AddRange(encryptedMessage);  // Encrypted message

                // Send the packet to the server
                await socket.SendAsync(new ArraySegment<byte>(packet.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }

        static async Task SendRaw(ClientWebSocket socket, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        static async Task ReceiveMessages(ClientWebSocket socket)
        {
            while (socket.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[4096];
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var data = buffer[..result.Count];

                try
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(data);
                        Console.WriteLine($"[Server]: {message}");
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Extract the encrypted key length (first 4 bytes)
                        int keyLen = BitConverter.ToInt32(data, 0);

                        // Extract the encrypted key (next `keyLen` bytes)
                        byte[] encryptedKey = data[4..(4 + keyLen)];

                        // Extract the encrypted message (remaining bytes after the encrypted key)
                        byte[] encryptedMessage = data[(4 + keyLen)..];

                        // Decrypt the AES key using recipient's RSA private key
                        byte[] aesKeyAndIv = RSAHelper.Decrypt(encryptedKey, recipientPrivateKey);  // Decrypt with RSA private key

                        // Extract the AES key and IV from the decrypted data (first 32 bytes for AES key, next 16 bytes for IV)
                        byte[] aesKey = aesKeyAndIv[..32];  // 256-bit AES key
                        byte[] iv = aesKeyAndIv[32..48];   // 128-bit IV

                        // Decrypt the message using AES
                        string decryptedMessage = AesHelper.Decrypt(encryptedMessage, iv, aesKey);

                        // Do something with the decrypted message, e.g., display it
                        Console.WriteLine($"Decrypted message: {decryptedMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to decrypt: {ex.Message}");
                }
            }
        }
    }
}
