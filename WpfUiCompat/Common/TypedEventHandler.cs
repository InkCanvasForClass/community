namespace WpfUiCompat.Common
{
    /// <summary>
    /// 泛型事件处理器委托（兼容 iNKORE TypedEventHandler，位于 Common 命名空间）。
    /// </summary>
    /// <typeparam name="TSender">事件发送者类型。</typeparam>
    /// <typeparam name="TResult">事件参数类型。</typeparam>
    /// <param name="sender">事件发送者。</param>
    /// <param name="result">事件参数。</param>
    public delegate void TypedEventHandler<TSender, TResult>(TSender sender, TResult result);
}