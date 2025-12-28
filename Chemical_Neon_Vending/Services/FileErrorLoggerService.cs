namespace Chemical_Neon.Services
{
    public class FileErrorLoggerService
    {
        private readonly string _logFilePath;

        public FileErrorLoggerService()
        {
            try
            {
                // Get the Downloads folder path for the current user
                string downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";

                // Check if the Downloads folder exists
                if (!Directory.Exists(downloadsFolder))
                {
                    Console.WriteLine("Downloads folder not found.");
                    throw new DirectoryNotFoundException("Downloads folder not found.");
                }

                // Combine with the log file name
                _logFilePath = Path.Combine(downloadsFolder, "error.log");

                Console.WriteLine($"Log file will be created at: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                throw; // Ensure you know the logger failed to initialize
            }
        }

        public void LogError(string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                // Handle exceptions while logging (e.g., log to a different medium if needed)
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }
}
