namespace NetworkingAssignmentServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TcpServer server = new TcpServer();
            server.Start();
            while (true)
            {
                server.Update();
            }
        }
    }
}
