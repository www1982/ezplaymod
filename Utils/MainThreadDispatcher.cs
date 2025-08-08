using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EZPlay.Utils
{
    /// <summary>
    /// 负责将来自其他线程的操作安全地调度到游戏主线程上执行，并能异步返回结果。
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly Queue<System.Action> ExecutionQueue = new Queue<System.Action>();

        public static Task<T> RunOnMainThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            System.Action action = () =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception e) { tcs.SetException(e); }
            };
            lock (ExecutionQueue) { ExecutionQueue.Enqueue(action); }
            return tcs.Task;
        }

        // 简化版，用于不需要返回值的操作 (现在返回Task以支持await)
        public static Task RunOnMainThread(System.Action act)
        {
            var tcs = new TaskCompletionSource<object>();
            System.Action action = () =>
            {
                try
                {
                    act();
                    tcs.SetResult(null);
                }
                catch (Exception e) { tcs.SetException(e); }
            };
            lock (ExecutionQueue) { ExecutionQueue.Enqueue(action); }
            return tcs.Task;
        }

        public static void ProcessQueue()
        {
            while (ExecutionQueue.Count > 0)
            {
                System.Action action;
                lock (ExecutionQueue) { action = ExecutionQueue.Dequeue(); }
                action.Invoke();
            }
        }
    }
}