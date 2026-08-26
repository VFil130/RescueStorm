using System.Collections.Generic;
using UnityEngine;

public class BTSelector : BTNode
{
    private List<BTNode> children;
    private int currentIndex = 0;

    public BTSelector(List<BTNode> children)
    {
        this.children = children;
    }

    public override Status Execute()
    {
        for (int i = 0; i < children.Count; i++)
        {
            Status status = children[i].Execute();
            if (DebugLog)
                Debug.Log($"[BT] Selector child {i} ({children[i].nodeName ?? children[i].GetType().Name}) -> {status}");
            if (status == Status.Running)
                return Status.Running;
            if (status == Status.Success)
                return Status.Success;
        }
        return Status.Failure;
    }
}