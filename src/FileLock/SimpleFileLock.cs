using System;
using System.Diagnostics;

namespace FileLock
{
    public class SimpleFileLock : IFileLock
    {        
        public TimeSpan LockTimeout { get; private set; }
        public string LockPath { get; private set; }

        protected SimpleFileLock(string lockPath, TimeSpan lockTimeout)
        {
            LockPath = lockPath;
            LockTimeout = lockTimeout;
        }

        public bool TryAcquireLock()
        {
            if (LockIO.LockExists(LockPath))
            {
                var lockContent = LockIO.ReadLock(LockPath);

                //Someone else has the lock file 'locked'
                if (lockContent.GetType() == typeof(OtherProcessOwnsFileLockContent))
                {
                    return false;
                }

                //the file no longer exists
                if (lockContent.GetType() == typeof(MissingFileLockContent))
                {
                    return AcquireLock();
                }

                var currentProcess = Process.GetCurrentProcess();

                //This lock belongs to this process - we can reacquire the lock
                if (lockContent.PID == currentProcess.Id && lockContent.MachineName.Equals(currentProcess.MachineName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return AcquireLock();
                }

                //The lock has not timed out - we won't attempt to acquire it
                var lockWriteTime = new DateTime(lockContent.Timestamp);
                
                if (!(Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds) > LockTimeout.TotalSeconds)) return false;
            }
            
            return AcquireLock();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ReleaseLock()
        {
            //Need to own the lock in order to release it (and we can reacquire the lock inside the current process)
            if (LockIO.LockExists(LockPath) && TryAcquireLock())
                LockIO.DeleteLock(LockPath);

            return true;
        }

        #region Internal methods

        protected FileLockContent CreateLockContent()
        {
            var process = Process.GetCurrentProcess();
            return new FileLockContent()
            {
                PID = process.Id,
                Timestamp = DateTime.Now.Ticks,
                ProcessName = process.ProcessName,
                MachineName = Environment.MachineName
            };
        }

        private bool AcquireLock()
        {
            return LockIO.WriteLock(LockPath, CreateLockContent());
        }

        #endregion

        #region Create methods

        public static SimpleFileLock Create(string lockName, TimeSpan lockTimeout)
        {
            Debug.Assert(lockName != null);
            Debug.Assert(lockName.Length > 0);

            if (string.IsNullOrEmpty(lockName))
                throw new ArgumentNullException(nameof(lockName), "Cannot be null or empty.");

            return new SimpleFileLock(lockName, lockTimeout) { LockPath = LockIO.GetFilePath(lockName) };
        }

        #endregion
    }
}