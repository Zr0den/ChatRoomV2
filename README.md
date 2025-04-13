## Features
CLI Messaging: Communicate with 1 other person/"yourself" by typing into a CLI.
WebSockets: We use WebSockets for our communication.

## Usage
1. Open the solution in Visual Studio
2. Build and run the ChatRoomServer
3. Run ChatRoomClient (Right click the project -> Debug -> Start new instance) twice (2x) times - or use dotnet run
4. Type stuff into the CLI once 2 Clients are connected

## Security Features
We use AES and RSA to encrypt our messages. 
Our messaging system uses E2EE Encryption - Messages are encrypted before they are sent, and can only be decrypted on the other end by the intended recipient.
We use Digital Signatures using RSA for authentication and to prevent impersonation
Our full flow is as following:
1. Each client generates an RSA Keypair on startup
2. Upon joining a channel, clients send their public key to the server.
3. The client's username is linked to the public key
4. The server exchanges the Public Keys between connected Clients.
5. Before sending a message, a random 256-bit AES key (sk) and IV is generated.
6. The message is encrypted with AES-256 using sk and IV (ciphertext).
7. sk is encrypted with the recipient's RSA public key (encKey).
8. The encrypted message is signed with the sender's digital signature
9. The encrypted message is sent as a structured JSON message to the server.
10. The server verifies the digital signature of the message, and that the sender's username and public key match
11. The server relays the message
12. The recipient Client decrypts encKey using their private RSA key to get the AES Key (sk) and IV 
13. The recipient Client decrypts ciphertext using sk and IV.

All in all, this in theory ensures:
A. Confidentiality (Only the intended recipient is able to read the messages - the Server only authenticates and relays messages) 
	Even if a message is intercepted, since the private RSA Key is required for decryption, the message can not be deciphered. 
	The server catches impersonation attempts by comparing public keys
B. Integrity, since tampered messages will fail not only due to the Digital Signature, but also due to the decryption process returning a garbage result if the content of the message is not exactly as it was on encryption.
	Because the signature is created using the randomly generated AES Key and IV, which can only be decrypted using the recipient's private RSA Key, tampering should not be possible.

Remark:
Regarding æ, ø og å: they become "?" not because of the encryption/decryption process, but because the console itself used in Visual Studio does not support those letters, even when using lines like.
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
And spending a lot of time trying to fix that/change to another console/something else, feels like it is out of the scope of what this assignment is meant for.