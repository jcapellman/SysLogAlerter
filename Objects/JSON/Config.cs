namespace SysLogAlerter.Objects.JSON
{
    public class Config
    {
        public string LogFilePath { get; set; }

        public string URL { get; set; }

        public Dictionary<string, string> Mapping { get; set; }
    }
}