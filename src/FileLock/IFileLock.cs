namespace FileLock
{
    public interface IFileLock
    {
        string LockPath { get; }

        bool TryAcquireLock();

        bool ReleaseLock();
    }
}
