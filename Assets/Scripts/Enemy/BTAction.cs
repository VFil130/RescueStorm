using System;
using UnityEngine;

public class BTAction : BTNode
{
    private Func<Status> action;

    public BTAction(Func<Status> action)
    {
        this.action = action;
    }

    public override Status Execute()
    {
        return action();
    }
}
