namespace CKKS_Agent_System.Config
{
    public class FileUploadOption
    {
        public const string SectionName = "FileUploadOption";

        public int AvatarUploadFileSize { get; set; }

        public int NeuralNetworkModelFileSize { get; set; }
    }
}
