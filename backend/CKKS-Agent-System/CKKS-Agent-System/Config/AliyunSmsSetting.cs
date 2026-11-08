namespace CKKS_Agent_System.Config
{
    public class AliyunSmsSetting
    {
        public const string SectionName = "AliyunSmsSetting";

        public string AccessKeyId { get; set; } = string.Empty;
        public string AccessKeySecret { get; set; } = string.Empty;
        public string SignName { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = string.Empty;
        public string Endpoint { get; set; } = "dypnsapi.aliyuncs.com"; // 默认值即可
    }
}
