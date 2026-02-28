using System;
using UnityEngine;

[Serializable]
public class PuzzleCondition
{
    public string puzzleId = "";
    
    public int expectedValue = 1;
    
    public float maxAgeSeconds = 0f;
}