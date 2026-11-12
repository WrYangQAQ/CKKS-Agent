namespace CKKS_Agent_System.Config
{
    public class VerificationSetting
    {
        public const string SectionName = "VerificationSetting";

        public int CodeExpirationMinutes { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 60;
        public int ChangePermitMinutes { get; set; } = 5;
    }
}
