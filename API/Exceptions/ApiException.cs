using System;

namespace EZPlay.API.Exceptions
{
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public new object Data { get; }

        public ApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }

        public ApiException(int statusCode, string message, object data) : base(message)
        {
            StatusCode = statusCode;
            Data = data;
        }
    }
}