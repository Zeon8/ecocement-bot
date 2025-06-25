namespace EcocementBot.Exceptions;

public class ClientNotFoundException : Exception
{
    public string ClientPhoneNumber { get; }

    public ClientNotFoundException(string clientPhoneNumber)
    {
        ClientPhoneNumber = clientPhoneNumber;
    }
}
