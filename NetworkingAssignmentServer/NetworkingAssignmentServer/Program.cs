using Network;

namespace NetworkingAssignmentServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //TcpServer server = new TcpServer();
            //server.Start();
            Server server = new Server();
            
            while (true)
            {

                server.Update();
                // TODO: make actual fixed update
                server.FixedUpdate();
                //server.Update();
            }
        }
    }
}
