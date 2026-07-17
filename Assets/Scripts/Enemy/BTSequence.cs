using System.Collections.Generic;
using UnityEngine;

public class BTSequence : BTNode
{
    private List<BTNode> children;
    private int currentIndex = 0;

    public BTSequence(List<BTNode> children)
    {
        this.children = children;
    }

    public override Status Execute()
    {
        for (int i = 0; i < children.Count; i++)
        {
            Status status = children[i].Execute();
            if (status == Status.Running)
                return Status.Running;
            if (status == Status.Failure)
                return Status.Failure;
        }
        return Status.Success;
    }
}