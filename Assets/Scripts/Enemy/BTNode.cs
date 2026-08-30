public abstract class BTNode
{
    public enum Status { Success, Failure, Running }
    public abstract Status Execute();

    public string nodeName;
}