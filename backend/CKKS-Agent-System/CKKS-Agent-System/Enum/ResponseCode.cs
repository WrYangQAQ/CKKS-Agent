namespace CKKS_Agent_System.Enum
{
    public enum ResponseCode
    {
        None = 0,
        Success = 200,

        BadRequest = 400,
        Unauthorized = 401,
        NotFound = 404,
        Conflict = 409,
        Forbidden = 403,
        DatabaseError = 500,

        InvalidCredentials = 1001,
        UserNotFound = 1002,
        UserNameAlreadyExists = 1003,
        EmailAlreadyExists = 1004,
        PhoneNumberAlreadyExists = 1005,
        TooManyVerifyCode = 1006,
        UnsupportedLoginType = 1010,
        UnsupportedVerifyType = 1011,
        RefreshTokenPersistError = 1012,
        InvalidParameter = 1013,
        InvalidVerificationCode = 1014,

        FileMissing = 2001,
        FileTooLarge = 2002,
        FileProcessingFailed = 2003,
        InvalidFileType = 2004,
        InvalidFileContent = 2005,
        InvalidInput = 2006,
        ImageUploadError = 2007,

        TempCoverUrlInvalid = 2101,    // 临时封面 URL 不合法
        TempCoverNotFound = 2102,      // 临时封面文件未找到
        TempQuotaExceeded = 2103,      // 临时区配额超出限制
        TempCoverMoveFailed = 2104,    // 临时封面移动失败

        UpdateFailed = 3001,
        UploadFailed = 3002,
        RegisterFailed = 3003,

        ArticleNotFound = 4001,
        TagsNotFound = 4002,
        TagAlreadyExists = 4003,
        CategoryNotFound = 4004,
        CategoryAlreadyExists = 4005,

        InternalError = 5000,


    }
}
