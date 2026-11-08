using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CKKS_Agent_System.Config
{
    public class BaseController : ControllerBase
    {
        public BaseController() { }

        // 根据 ResponseCode 转换为对应的 HTTP 状态码，省去 Controller 中大量的 if-else 语句
        private int ToHttpStatusCode(ResponseCode code)
        {
            return code switch
            {
                ResponseCode.Success => StatusCodes.Status200OK,

                ResponseCode.BadRequest => StatusCodes.Status400BadRequest,
                ResponseCode.Unauthorized => StatusCodes.Status401Unauthorized,
                ResponseCode.Forbidden => StatusCodes.Status403Forbidden,
                ResponseCode.NotFound => StatusCodes.Status404NotFound,
                ResponseCode.Conflict => StatusCodes.Status409Conflict,
                ResponseCode.DatabaseError => StatusCodes.Status500InternalServerError,

                ResponseCode.InvalidCredentials => StatusCodes.Status401Unauthorized,
                ResponseCode.UserNotFound => StatusCodes.Status404NotFound,
                ResponseCode.UserNameAlreadyExists => StatusCodes.Status409Conflict,
                ResponseCode.EmailAlreadyExists => StatusCodes.Status409Conflict,
                ResponseCode.PhoneNumberAlreadyExists => StatusCodes.Status409Conflict,
                ResponseCode.UnsupportedLoginType => StatusCodes.Status400BadRequest,
                ResponseCode.UnsupportedVerifyType => StatusCodes.Status400BadRequest,
                ResponseCode.RefreshTokenPersistError => StatusCodes.Status500InternalServerError,
                ResponseCode.InvalidParameter => StatusCodes.Status400BadRequest,
                ResponseCode.InvalidVerificationCode => StatusCodes.Status400BadRequest,
                ResponseCode.TooManyVerifyCode => StatusCodes.Status429TooManyRequests,

                ResponseCode.FileMissing => StatusCodes.Status400BadRequest,
                ResponseCode.FileTooLarge => StatusCodes.Status413PayloadTooLarge,
                ResponseCode.FileProcessingFailed => StatusCodes.Status500InternalServerError,
                ResponseCode.InvalidFileType => StatusCodes.Status400BadRequest,
                ResponseCode.InvalidFileContent => StatusCodes.Status400BadRequest,
                ResponseCode.InvalidInput => StatusCodes.Status400BadRequest,
                ResponseCode.ImageUploadError => StatusCodes.Status500InternalServerError,

                ResponseCode.TempCoverUrlInvalid => StatusCodes.Status400BadRequest,
                ResponseCode.TempCoverNotFound => StatusCodes.Status404NotFound,
                ResponseCode.TempQuotaExceeded => StatusCodes.Status400BadRequest,
                ResponseCode.TempCoverMoveFailed => StatusCodes.Status500InternalServerError,

                ResponseCode.UpdateFailed => StatusCodes.Status500InternalServerError,
                ResponseCode.UploadFailed => StatusCodes.Status500InternalServerError,
                ResponseCode.RegisterFailed => StatusCodes.Status400BadRequest,

                ResponseCode.ArticleNotFound => StatusCodes.Status404NotFound,
                ResponseCode.TagsNotFound => StatusCodes.Status404NotFound,
                ResponseCode.TagAlreadyExists => StatusCodes.Status409Conflict,
                ResponseCode.CategoryNotFound => StatusCodes.Status404NotFound,
                ResponseCode.CategoryAlreadyExists => StatusCodes.Status409Conflict,

                ResponseCode.InternalError => StatusCodes.Status500InternalServerError,
                ResponseCode.None => StatusCodes.Status500InternalServerError,

                _ => StatusCodes.Status500InternalServerError
            };
        }

        // 根据状态码自动返回对应的 HTTP 响应，简化 Controller 代码
        protected IActionResult ReturnResponse(ApiResponse response)
        {
            // 成功时直接返回 200 OK
            if (response.Success)
            {
                return Ok(response);
            }

            // 失败时根据 ResponseCode 映射 HTTP 状态码
            int statusCode = ToHttpStatusCode(response.Code);
            return StatusCode(statusCode, response);
        }
    }
}