using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

class Server
{
    static void Main(string[] args)
    {

        // create a TCP/IP socket
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // bind the socket to a local endpoint and start listening
        listener.Bind(new IPEndPoint(IPAddress.Any, 8888));
        listener.Listen(10);

        Console.WriteLine("Server started. Listening for incoming connections...");
        List<ClientInfo> _clients = new List<ClientInfo>();

        while (true)
        {
            // accept incoming connections
            Socket handler = listener.Accept();
            Console.WriteLine("Client connected: {0}", handler.RemoteEndPoint.ToString());
            // start a new thread to handle the client
            ClientHandler clientHandler = new ClientHandler(handler, ref _clients);
            clientHandler.Start();
        }
    }
}

class ClientHandler
{
    private Socket _socket;
    static List<ClientInfo> _clients;

    public ClientHandler(Socket socket, ref List<ClientInfo> clients)
    {
        _socket = socket;
        _clients = clients;
    }

    public void Start()
    {
        // authenticate the client
        bool isAuthenticated = false;
        string clientId = "";
        while (!isAuthenticated)
        {
            // receive request from the client
            byte[] buffer = new byte[1024];
            int bytesRead = _socket.Receive(buffer);
            string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            Console.WriteLine("Received request from {0}: {1}", _socket.RemoteEndPoint.ToString(), message);

            // parse the request and handle it according to the protocol
            string[] parts = message.Split(':');

            if (parts.Length < 3)
            {
                // invalid request format
                byte[] response = Encoding.ASCII.GetBytes("invalid request format");
                _socket.Send(response);
                continue;
            }

            string requestType = parts[0];
            string username = parts[1];
            string password = parts[2];

            switch (requestType)
            {
                case "login":
                    if (IsValidCredentials(username, password, false))
                    {
                        isAuthenticated = true;
                        clientId = username;
                        byte[] responseBuffer = Encoding.ASCII.GetBytes("login_ok");
                        _socket.Send(responseBuffer);
                    }
                    else
                    {
                        byte[] responseBuffer = Encoding.ASCII.GetBytes("invalid username or password");
                        _socket.Send(responseBuffer);
                    }
                    break;

                case "signup":
                    bool isSignUpSuccessful = IsValidCredentials(username, password, true);

                    if (isSignUpSuccessful)
                    {
                        byte[] responseBuffer = Encoding.ASCII.GetBytes("signup_ok");
                        _socket.Send(responseBuffer);
                    }
                    else
                    {
                        byte[] responseBuffer = Encoding.ASCII.GetBytes("username already exists");
                        _socket.Send(responseBuffer);
                        continue;
                    }
                    break;

                default:
                    // unknown request type
                    byte[] unknownResponse = Encoding.ASCII.GetBytes("unknown request type");
                    _socket.Send(unknownResponse);
                    continue;
            }
        }

        // add the client to the collection of clients
        ClientInfo client = new ClientInfo();
        client.ID = clientId;
        client.Socket = _socket;
        _clients.Add(client);

        Console.WriteLine("Client {0} authenticated. Assigned ID {1}", _socket.RemoteEndPoint.ToString(), clientId);

        // broadcast a message to all clients that a new client has connected
        string connectMessage = $"{clientId} has joined the chat.";

        foreach (var c in _clients)
        {
            if (c.Socket != _socket)
            {
                byte[] buffer = Encoding.ASCII.GetBytes(connectMessage);
                c.Socket.Send(buffer);
            }
        }

        // start a new thread to handle the client
        new System.Threading.Thread(() =>
        {
            while (true)
            {
                // receive data from the client
                byte[] buffer = new byte[1024];
                int bytesRead = _socket.Receive(buffer);
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                Console.WriteLine("Received message from {0}: {1}", _socket.RemoteEndPoint.ToString(), message);

                // parse the message and handleit according to the protocol
                string[] parts = message.Split(':');

                if (parts.Length < 2)
                {
                     // broadcast a message to all clients that a client has disconnected
                            if (message.ToLower() == "logout")
                            {
                                _clients.Remove(client);
                                string disconnectMessage = $"{clientId} has left the chat.";
                                foreach (var c in _clients)
                                {
                                    if (c.Socket != _socket)
                                    {
                                        byte[] buffer2 = Encoding.ASCII.GetBytes(disconnectMessage);
                                        c.Socket.Send(buffer2);
                                    }
                                }
                                break;
                            }
                    else { // invalid message format
                    byte[] response = Encoding.ASCII.GetBytes("invalid message format");
                    _socket.Send(response);
                    continue;}
                }
                else
                {
                    string Sender = parts[0];
                    string recipient = parts[1];
                    string content = parts[2];

                    switch (recipient)
                    {
                        case "bc":
                            // broadcast the message to all connected clients except the sender
                            foreach (var client in _clients)
                            {
                                if (client.Socket != _socket) // don't send the message back to the sender
                                {
                                    byte[] buffer1 = Encoding.ASCII.GetBytes(Sender + ":bc:" + content);
                                    Console.WriteLine(buffer1);
                                    
                                    client.Socket.Send(buffer1);
                                }
                            }
                            break;

                        // add more cases for other commands or message types as needed

                        default:
                            // send the message to the specified recipient
                            ClientInfo recipientClient = _clients.Find(c => c.ID == recipient);
                            if (recipientClient == null)
                            {
                                // recipient not found
                                byte[] response1 = Encoding.ASCII.GetBytes("Recipient not found");
                                _socket.Send(response1);
                            }
                            else
                            {
                                byte[] buffer2 = Encoding.ASCII.GetBytes(Sender + ":" + recipient + ":" + content);
                                recipientClient.Socket.Send(buffer2);
                            }
                            break;
                            // send a response back to the client
                            byte[] response = Encoding.ASCII.GetBytes("Message received");
                            _socket.Send(response);

                           
                    }

                }
            }
        }).Start();
    }
    private bool IsValidCredentials(string username, string password, bool isSignUp)
    {
        // create a MongoDB client and database
        MongoClient client = new MongoClient("mongodb+srv://19p3041:admin123@cluster0.lzbu4ip.mongodb.net/");
        IMongoDatabase database = client.GetDatabase("GameDB");

        // get the users collection
        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("Player");

        // define a filter to find the user with the specified username
        var filter = Builders<BsonDocument>.Filter.Eq("UserName", username);

        // find the user in the database
        var result = collection.Find(filter).FirstOrDefault();

        if (result == null && isSignUp)
        {
            // user doesn't exist and signup is requested
            var newUser = new BsonDocument
        {
            { "UserName", username },
            { "Password", password }
        };

            collection.InsertOne(newUser);

            // return true to indicate successful signup
            return true;
        }
        else if (result != null && !isSignUp)
        {
            // user exists and login is requested
            var userPassword =result.GetValue("Password").ToString();
            return (userPassword == password);
        }
        else
        {
            // user doesn't exist and login is requested, or user exists and signup is requested
            return false;
        }
    }


}

class ClientInfo
{
    //username of this client
    public string ID { get; set; }
    public Socket Socket { get; set; }
}


