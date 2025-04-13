using Cryptography;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Sockets;
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
        private static string recipientPublicKey = string.Empty;  // Store the recipient's public key
        private static string username;

        static async Task Main()
        {
            Random rnd = new Random();
            username = $"User{rnd.Next(1000, 9999)}";

            // Generate RSA keys and public key
            var publicKey = rsa.PublicKey;

            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
            Console.WriteLine("Connected to server. Hello " + username + ", you may now type");

            // Receive messages in the background
            _ = Task.Run(() => ReceiveMessages(socket));

            // Send public key and username to server
            await SendRaw(socket, publicKey);
            await SendRaw(socket, username);

            while (socket.State == WebSocketState.Open)
            {
                var message = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(message)) continue;

                message = $"{username}: " + message;
                SendMessage(message, recipientPublicKey, socket);
            }
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
                        if (message.Substring(0, 3) == "PK:")
                        {
                            recipientPublicKey = message.Substring(3);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        string json = Encoding.UTF8.GetString(data);
                        var payload = JsonConvert.DeserializeObject<EncryptedPayload>(json);

                        if (payload != null)
                        {
                            byte[] encryptedKey = Convert.FromBase64String(payload.EncryptedKey);
                            byte[] encryptedMessage = Convert.FromBase64String(payload.EncryptedMessage);
                            byte[] aesIV = Convert.FromBase64String(payload.IV);

                            string decrypted = DecryptMessage(encryptedKey, encryptedMessage, rsa);

                            bool isValid = RSAHelper.VerifySignature(
                                Encoding.UTF8.GetBytes(decrypted),
                                Convert.FromBase64String(payload.Signature),
                                payload.SenderPublicKey
                            );

                            if (isValid)
                            {
                                Console.WriteLine($"\n{decrypted}\n");
                            }
                            else
                            {
                                Console.WriteLine($"WARNING: Tampered message received - {decrypted}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to decrypt: {ex.Message}");
                }
            }
        }

        public static string DecryptMessage(byte[] encryptedKey, byte[] encryptedMessage, RSAHelper rsaHelper)
        {
            // Decrypt the AES key and IV with the private RSA key
            byte[] aesKeyAndIv = rsaHelper.DecryptRaw(encryptedKey);
            byte[] aesKey = aesKeyAndIv.Take(32).ToArray(); // 256-bit AES key
            byte[] aesIV = aesKeyAndIv.Skip(32).Take(16).ToArray(); // 128-bit AES IV

            // Decrypt the message using AES
            return AesHelper.Decrypt(encryptedMessage, aesKey, aesIV);
        }

        public static async void SendMessage(string message, string recipientPublicKey, ClientWebSocket socket)
        {
            // Encrypt the message with AES
            (byte[] encryptedMessage, byte[] aesIV, byte[] aesKey) = AesHelper.Encrypt(message);

            // Encrypt the AES key and IV with the recipient's RSA public key
            byte[] aesKeyAndIv = aesKey.Concat(aesIV).ToArray();
            byte[] encryptedKey = RSAHelper.Encrypt(aesKeyAndIv, recipientPublicKey);

            // Sign the plaintext message
            byte[] signature = rsa.SignData(Encoding.UTF8.GetBytes(message));

            // Send the encrypted message and encrypted key to the server
            var jsonMessage = new
            {
                EncryptedKey = Convert.ToBase64String(encryptedKey),
                EncryptedMessage = Convert.ToBase64String(encryptedMessage),
                IV = Convert.ToBase64String(aesIV),
                Signature = Convert.ToBase64String(signature),
                SenderPublicKey = rsa.PublicKey
            };

            await SendRaw(socket, JsonConvert.SerializeObject(jsonMessage));
        }

        static async Task SendRaw(ClientWebSocket socket, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}
