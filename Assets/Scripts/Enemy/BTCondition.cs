using System;
using UnityEngine;

public class BTCondition : BTNode
{
    private Func<bool> condition;

    public BTCondition(Func<bool> condition)
    {
        this.condition = condition;
    }

    public override Status Execute()
    {
        return condition() ? Status.Success : Status.Failure;
    }
}