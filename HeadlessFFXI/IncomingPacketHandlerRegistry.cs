using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HeadlessFFXI;

public class PacketHandlerRegistry
{
    private readonly Dictionary<ushort, IPacketHandler> _handlers = new();
    private readonly string _handlerFolder;

    public PacketHandlerRegistry(string handlerFolder = "IncomingPackets")
    {
        _handlerFolder = handlerFolder;
        RegisterAllHandlers();
    }

    private void RegisterAllHandlers()
    {
        var handlerType = typeof(IPacketHandler);
        var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => handlerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IPacketHandler)Activator.CreateInstance(t)!);

        foreach (var handler in handlers)
        {
            _handlers[handler.PacketId] = handler;
        }

        Console.WriteLine($"[Init] Registered {_handlers.Count} packet handlers.");
    }

    public bool TryHandle(Client client, ushort packetId, ReadOnlySpan<byte> data)
    {
        if (_handlers.TryGetValue(packetId, out var handler))
        {
            handler.Handle(client, data);
            return true;
        }

        Console.WriteLine($"[Game] Unhandled packet 0x{packetId:X2} (size {data.Length})");
        GenerateHandlerStub(packetId);
        return false;
    }
        private void GenerateHandlerStub(ushort packetId)
    {
        try
        {
            Directory.CreateDirectory(_handlerFolder);

            string fileName = Path.Combine(_handlerFolder, $"P0x{packetId:X3}.cs");
            if (File.Exists(fileName))
                return;

            string className = $"P{packetId:X3}Handler";

            string stub = $@"//
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x{packetId:X4}
//
using System;
using HeadlessFFXI;

public class {className} : IPacketHandler
{{
    public ushort PacketId => 0x{packetId:X};

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {{
        Console.WriteLine(""[{className}] Handler not yet implemented. Size: {{data.Length}}"");
        // TODO: Implement handler logic here
    }}
}}";

            File.WriteAllText(fileName, stub, Encoding.UTF8);
            Console.WriteLine($"[AutoGen] Created {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to auto-generate handler: {ex.Message}");
        }
    }
}

public interface IPacketHandler
{
    ushort PacketId { get; }
    void Handle(Client client, ReadOnlySpan<byte> data);
}
