using UnityEngine;

public class BTInverter : BTNode
{
    private BTNode child;

    public BTInverter(BTNode child)
    {
        this.child = child;
    }

    public override Status Execute()
    {
        if (child == null) return Status.Failure;

        Status status = child.Execute();

        if (status == Status.Running)
            return Status.Running;

        return status == Status.Success ? Status.Failure : Status.Success;
    }
}