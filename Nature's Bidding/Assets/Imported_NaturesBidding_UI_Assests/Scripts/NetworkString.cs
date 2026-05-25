using Unity.Netcode;

/// <summary>
/// NetworkString — A NetworkVariable-safe string wrapper.
/// Keep this in its own file so all scripts can reference it.
/// </summary>
[System.Serializable]
public struct NetworkString : INetworkSerializable
{
    public string Value;

    public NetworkString(string val) { Value = val ?? ""; }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Value);
    }

    public static implicit operator NetworkString(string s) => new NetworkString(s);
    public override string ToString() => Value ?? "";
}