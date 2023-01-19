using ReserveBlockCore.Utilities;
using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Extensions
{
    public static class TaskExtensions
    {
		public static async Task WhenAtLeast<T>(this IEnumerable<Task<T>> tasks, Func<T, bool> successPredicate, int atLeast)
		{
			var TaskSource = new TaskCompletionSource();

			var NumOfTasksCompleted = 0;
			foreach (var task in tasks)
				_ = task.ContinueWith(async x =>
				{
					if (successPredicate(await x) || !x.IsCompletedSuccessfully)
						Interlocked.Increment(ref NumOfTasksCompleted);

					if (NumOfTasksCompleted >= atLeast)
						TaskSource.TrySetResult();

				});

			await TaskSource.Task;
		}

		public static async Task WhenAtLeast(this IEnumerable<Task> tasks, Func<bool> successPredicate, int atLeast)
		{
			var TaskSource = new TaskCompletionSource<object>();

			var NumOfTasksCompleted = 0;
			foreach (var task in tasks)
				_ = task.ContinueWith(async x =>
				{
					if (successPredicate())
						Interlocked.Increment(ref NumOfTasksCompleted);

					if (NumOfTasksCompleted >= atLeast)
						TaskSource.TrySetResult(null);

				});

			await TaskSource.Task;
		}
		public static async Task<T> RetryUntilSuccessOrCancel<T>(this Func<Task<T>> func, Func<T, bool> success, int retryDelay, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
                var delay = retryDelay > 0 ? Task.Delay(retryDelay) : null;
                try
				{					
					var Result = await func();
					if (success(Result))
						return Result;
				}
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "TaskExtensions.RetryUntilSuccessOrCancel()");
                }
                if (delay != null)
                    await delay;
            }

			return default;
		}

        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();
            cancellationToken.Register(s => ((TaskCompletionSource)s).TrySetResult(), tcs);
            return tcs.Task;
        }

    }
}
