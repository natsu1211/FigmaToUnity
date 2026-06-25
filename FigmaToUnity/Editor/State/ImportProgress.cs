namespace FigmaToUnity.Editor.State
{
    internal sealed class ImportProgress
    {
        public ImportProgress(string stageName, string message, int completedItems, int totalItems, bool isIndeterminate, bool canCancel)
        {
            StageName = stageName;
            Message = message;
            CompletedItems = completedItems;
            TotalItems = totalItems;
            IsIndeterminate = isIndeterminate;
            CanCancel = canCancel;
        }

        public string StageName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int CompletedItems { get; set; }
        public int TotalItems { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool CanCancel { get; set; }

        public float Percentage
        {
            get
            {
                if (IsIndeterminate || TotalItems <= 0)
                {
                    return 0f;
                }

                return (float)CompletedItems / TotalItems;
            }
        }
    }
}
