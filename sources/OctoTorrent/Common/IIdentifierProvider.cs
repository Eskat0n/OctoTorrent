namespace OctoTorrent.Common
{
    public interface IIdentifierProvider
    {
        string CreatePeerId();

        string CreateHumanReadableId();

        string CreateDhtClientVersion();
    }
}