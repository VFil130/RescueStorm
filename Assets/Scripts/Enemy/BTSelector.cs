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
        for (int i = currentIndex; i < children.Count; i++)
        {
            Status status = children[i].Execute();
            if (status == Status.Running)
            {
                currentIndex = i;
                return Status.Running;
            }
            if (status == Status.Success)
            {
                currentIndex = 0;
                return Status.Success;
            }
        }
        currentIndex = 0;
        return Status.Failure;
    }
}