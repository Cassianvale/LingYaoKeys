namespace WpfApp.Services.Events
{
    /// <summary>
    /// 状态消息事件参数
    /// </summary>
    public class StatusMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public bool IsError { get; }

        public StatusMessageEventArgs(string message, bool isError = false)
        {
            Message = message;
            IsError = isError;
        }
    }
} 