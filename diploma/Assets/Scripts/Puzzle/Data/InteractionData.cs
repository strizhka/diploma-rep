[System.Serializable]
public struct InteractionData
{
    public string ObjectId;
    public string NewState;

    public InteractionData(string objectId, string newState)
    {
        ObjectId = objectId;
        NewState = newState;
    }
}