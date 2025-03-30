## Features
Channel Creation: Users can create new chat channels.
Invite Keys: When a channel is created, an invite key is generated, which can be shared with others to join the channel securely.
Confidentiality and Integrity: All communications are encrypted using AES (Advanced Encryption Standard) and E2EE (End-to-End Encryption).
Join Channels: Users can join existing channels by entering the correct invite key.
WebSockets: We use WebSockets for our communication.

## Usage
1. Open the solution in Visual Studio
2. Build and run the ChatRoomServer
3. Run ChatRoomClient (Right click the project -> Debug -> Start new instance)
4. Repeat step 3 as many times as you want - 1 instance = 1 user
5. Follow the the instructions on the User CLI's (type stuff into it) - It will tell you when and how to do it all


## Security Features
We use AES to encrypt our messages. 
Our messages are encrypted before they are sent, and decrypted upon being received. This means that this is an implementation of E2EE.
To decrypt a message, you need both the Key and the IV (Initialization Vector). IV's are unique and randomly generated on each encryption. 
The IV is also part of the encryption process and is sent as part of the message in order to decrypt it on the other end.
Messages from and to the server are also encrypted, so that things like the invite key etc. can't be intercepted.
This means that even if a message is intercepted between sender and receiver (or by the server), it is not possible to read its content.
All in all, this in theory ensures Confidentiality (Only the intended recipients are able to read the messages).
Integrity is mostly ensured by the decryption process failing should the cipher text be modified (we don't have any code making checks/handling this currently though)

Remarks:
Right now we just store our Key in plaintext in our "AesHelper" file, which is obviously not what you want to do in a serious situation, but we figured it was fine for demonstration purposes
In theory, the IV's help mitigate it a little, as the Key alone is not enough, but obviously securing the key is just the primary concern here.