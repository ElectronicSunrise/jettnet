# <img src="/img~/jnet.png"/> 

# JETTNET Messaging System

- Easily connect to a server/client 
- Communicate via Messages
- Messages can be pre-defined structs OR prodecural

# Example Usage

- Define a Message

```csharp
    // struct way
    public struct MyData : IJettMessage
    {
        public int Integer;

        public void Serialize(JettWriter writer)
        {
            writer.WriteInt32(Integer);
        }

        public MyData Deserialize(JettReader reader)
        {
            return new MyData { Integer = reader.ReadInt32() }
        }
    }

    // functional way
    _server.Send("MyData", (writer) =>
    {
        writer.WriteInt32(200);
    });
```

- Connect to Server and Send Data

```csharp
    JettClient _client = new JettClient(new KcpSocket());

    _client.OnConnect += () => { _client.Send(new MyData { Integer = 200 }); }; 

    _client.Connect("127.0.0.1");
```

- Register Message on Server and Handle

```csharp
    ushort port = 7777;

    JettServer _server = new JettServer(port, new KcpSocket());

    _server.ClientConnectedToServer += (connection) => { Log.Debug("New client connected") };

    _server.Register<MyData>((data, sender) =>
    {
        Log.Debug("Connection says: " + data.Integer);
    });

    _server.Start();
```

- Poll Events in your Update Loop

```csharp
    private void Update()
    {
        _server?.FetchIncoming();
        _server?.SendOutgoing();

        _client?.FetchIncoming();
        _client?.SendOutgoing();
    }
```


