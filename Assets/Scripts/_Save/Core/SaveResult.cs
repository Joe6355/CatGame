namespace CatGame.SaveSystem
{
    public enum SaveOperationStatus
    {
        Success,
        SlotMissing,
        Corrupted,
        VersionTooNew,
        StorageError,
        Cancelled
    }

    public sealed class SaveResult
    {
        public SaveOperationStatus status;
        public string message;

        public bool IsSuccess
        {
            get { return status == SaveOperationStatus.Success; }
        }

        public static SaveResult Success(string message)
        {
            return new SaveResult { status = SaveOperationStatus.Success, message = message };
        }

        public static SaveResult Fail(SaveOperationStatus status, string message)
        {
            return new SaveResult { status = status, message = message };
        }
    }
}
