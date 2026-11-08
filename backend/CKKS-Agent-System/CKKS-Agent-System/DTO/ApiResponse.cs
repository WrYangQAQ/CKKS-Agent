using CKKS_Agent_System.Enum;

namespace CKKS_Agent_System.DTO
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public ResponseCode Code { get; set; }

        public ApiResponse() { }

        public ApiResponse(bool success, string message, object? data = null, ResponseCode code = ResponseCode.None)
        {
            Success = success;
            Message = message;
            Data = data;
            Code = code;
        }
    }
}
