using System;
using System.Collections.Generic;
using System.Text;

namespace Test.Utility
{
    /// <summary>
    /// 
    /// </summary>
    /// <see cref="https://docs.microsoft.com/en-us/azure/azure-functions/functions-test-a-function"/>
    public class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();

        private NullScope() { }

        public void Dispose() { }
    }
}
