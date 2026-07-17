using UnityEngine;

public class BTLoop : BTNode
{
    private BTNode child;
    private int repeatCount;
    private int currentCount = 0;
    private bool infinite;

    public BTLoop(BTNode child, int repeatCount = -1)
    {
        this.child = child;
        this.repeatCount = repeatCount;
        this.infinite = repeatCount == -1;
    }

    public override Status Execute()
    {
        if (child == null) return Status.Failure;

        while (true)
        {
            Status status = child.Execute();

            if (status == Status.Running)
                return Status.Running;

            if (status == Status.Failure)
            {
                currentCount = 0;
                return Status.Failure;
            }

            if (status == Status.Success)
            {
                if (infinite)
                    continue;

                currentCount++;
                if (currentCount >= repeatCount)
                {
                    currentCount = 0;
                    return Status.Success;
                }
                continue;
            }
        }
    }
}