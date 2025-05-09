using System.Text;
using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Utilities;
using Newtonsoft.Json;

namespace Helios.XMPP.Roots;

public class AuthHandler
{
    public static async Task HandleAsync(IWebSocketConnection socket, ClientSessions client, XmppMessage root)
    {
        if (root is null || root.Element is null)
        {
            socket.Close();
            return;
        }
        
        byte[] decodedBytes = Convert.FromBase64String(root.Element.Value);
        string decodedContent = Encoding.UTF8.GetString(decodedBytes);
        
        string[] authFields = decodedContent.Split('\0');
        if (authFields.Length < 2)
        {
            socket.Close();
            return;
        }

        string accountId = authFields[1];

        var userRepository = Constants.repositoryPool.Repo<User>();
        var clientSessionsRepository = Constants.repositoryPool.Repo<ClientSessions>();
        
        if (await clientSessionsRepository().FindAsync(new ClientSessions { AccountId = accountId }) != null)
        {
            socket.Close();
            return;
        }
        
        var user = await userRepository().FindAsync(new User { AccountId = accountId });
        if (user == null || user.Banned)
        {
            socket.Send(CreateFailureResponse("not-authorized", "Password not verified"));
            return;
        }

        client.AccountId = accountId;
        client.Token = authFields[2];
        client.DisplayName = user.Username;
        client.IsAuthenticated = true;

        await clientSessionsRepository().UpdateAsync(client);
        
        Logger.Info($"New XMPP Client logged in as {user.Username}");
        socket.Send(new XElement(
            XNamespace.Get("urn:ietf:params:xml:ns:xmpp-sasl") + "success"
        ).ToString());
    }

    private static string CreateFailureResponse(string condition, string message)
    {
        XNamespace ns = XNamespace.Get("urn:ietf:params:xml:ns:xmpp-sasl");

        XDocument doc = new XDocument(
            new XElement(ns + "failure",
                new XElement(ns + condition),
                new XElement(ns + "text",
                    new XAttribute(XNamespace.Xml + "lang", "eng"),
                    message
                )
            )
        );
        return doc.ToString();
    }
}