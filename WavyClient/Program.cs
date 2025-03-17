using System.Net.Sockets;
using System.Text;

class Wavy
{
    static void Main()
    {
        TcpClient client = new TcpClient("127.0.0.1", 5000);
        NetworkStream stream = client.GetStream();
        byte[] message = Encoding.UTF8.GetBytes("WAVY ID 001");
        stream.Write(message, 0, message.Length);

        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        Console.WriteLine("Servidor respondeu: " + Encoding.UTF8.GetString(buffer, 0, bytesRead));
    }
}