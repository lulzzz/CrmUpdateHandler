namespace CrmUpdateHandler.Utility
{
    using System;

    /// <summary>
    /// An Exception unique to this function app that is distinct from System.Exception. It carries 
    /// extra information to help with support
    /// </summary>
    public class CrmUpdateHandlerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the CrmUpdateHandlerException class.
        /// </summary>
        public CrmUpdateHandlerException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the CrmUpdateHandlerException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public CrmUpdateHandlerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CrmUpdateHandlerException class with a specified error message
        /// and a reference to the exception that is the original source of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a 
        /// null reference if no inner exception is specified</param>
        public CrmUpdateHandlerException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CrmUpdateHandlerException class with a specified error message
        /// and a reference to the exception that is the original source of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a 
        /// null reference if no inner exception is specified</param>
        /// <param name="whatHappened">a user-friendly description of the problem</param>
        /// <param name="whatIsAffected">a string describing the effects of the problem</param>
        /// <param name="whatToDo">suggestions for the user on how the problem can be fixed</param>
        /// <param name="supportInfo">a string containing support info (record id's, security tokens, etc)</param>
        public CrmUpdateHandlerException(string message, Exception inner, string whatHappened, string whatIsAffected, string whatToDo, string supportInfo)
            : base(message, inner)
        {
            this.WhatHappened = whatHappened;
            this.WhatIsAffected = whatIsAffected;
            this.WhatToDo = whatToDo;
            this.SupportInfo = supportInfo;
        }

        /// <summary>
        /// Gets or sets a user-friendly description of the problem
        /// </summary>
        public string WhatHappened { get; set; }

        /// <summary>
        /// Gets or sets a string describing the effects of the problem
        /// </summary>
        public string WhatIsAffected { get; set; }

        /// <summary>
        /// Gets or sets a string with suggestions for the user on how the problem can be fixed
        /// </summary>
        public string WhatToDo { get; set; }

        /// <summary>
        /// Gets or sets a string containing support info (record id's, security tokens, etc)
        /// </summary>
        public string SupportInfo { get; set; }
    }
}
