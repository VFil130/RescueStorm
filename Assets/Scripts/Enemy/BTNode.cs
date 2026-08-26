using UnityEngine;

public abstract class BTNode
{
    public enum Status { Success, Failure, Running }
    public static bool DebugLog;
    public abstract Status Execute();

    protected string nodeName;
    public void SetName(string name) => nodeName = name;
}