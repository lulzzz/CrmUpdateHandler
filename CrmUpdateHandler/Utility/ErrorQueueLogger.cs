using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    using Microsoft.Azure.WebJobs;

    /// <summary>
    /// A class to add some syntactic sweetness when logging to an error queue
    /// </summary>
    internal class ErrorQueueLogger
    {
        /// <summary>
        /// The async error queue reference that finishes up enqueuing messages
        /// </summary>
        private IAsyncCollector<string> errorQueue;

        private string solutionName;

        /// <summary>
        /// Retains the name of the function that instantiated this logger, for easy identification in log messages.
        /// </summary>
        private string functionName;

        public ErrorQueueLogger(
            IAsyncCollector<string> errorQueue,
            string solutionName,
            string functionName)
        {
            this.errorQueue = errorQueue;
            this.solutionName = solutionName;
            this.functionName = functionName;
        }

        public async void LogError(string format, params object[] args)
        {
            var msg = string.Format(format, args);
            await this.errorQueue.AddAsync(this.solutionName + "." + this.functionName + ": " + msg);
        }
    }
}
