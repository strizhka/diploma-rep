using System;
using Mirror;

[Serializable]
public struct PuzzleState
{
    public int Value;
    public double LastChangeServerTime;

    public PuzzleState(int value)
    {
        Value = value;
        LastChangeServerTime = 0d;
    }
}