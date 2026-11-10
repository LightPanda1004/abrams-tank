using Mirror;

public class OwnerNetworkTransform : NetworkTransform
{
    public override bool OnCheckAuthority(NetworkConnectionToClient conn)
    {
        return base.connectionToClient == conn;
    }
}
