namespace OctoTorrent.Tests.Client
{
    using System.IO;
    using NUnit.Framework;

    [TestFixture]
    [Ignore]
    public class FileManagerTest
    {
        private string _path = string.Empty;
        private string _directoryName = string.Empty;
        private string _fullPath;

        [SetUp]
        public void Setup()
        {
            _path = GetType().Assembly.Location;
            for (var i = 0; i >= 0; i++)
            {
                var directoryName = string.Format("temp{0}", i);

                if (Directory.Exists(directoryName)) 
                    continue;

                _directoryName = directoryName;
                _fullPath = Path.Combine(_path, directoryName);
                Directory.CreateDirectory(_fullPath);

                break;
            }

            GenerateTestFiles();
        }

        private void GenerateTestFiles()
        {
            const string data = "this is my teststring. It's not really that long, but i'll be writing a lot more where this come from\r\n";

            var file1 = File.OpenWrite(Path.Combine(_fullPath, "file1.txt"));
            var file2 = File.OpenWrite(Path.Combine(_fullPath, "file2.txt"));

            for (var i = 0; i < 100; i++)
                file1.Write(System.Text.Encoding.UTF8.GetBytes(data), 0, System.Text.Encoding.UTF8.GetByteCount(data));

            for (var i = 0; i < 5000; i++)
                file2.Write(System.Text.Encoding.UTF8.GetBytes(data), 0, System.Text.Encoding.UTF8.GetByteCount(data));

            file1.Close();
            file2.Close();
        }

        [TearDown]
        public void RemoveTempFiles()
        {
            foreach (var filePath in Directory.GetFiles(Path.Combine(_path, _directoryName)))
                File.Delete(filePath);

            Directory.Delete(Path.Combine(_path, "temp"));
        }
    }
}