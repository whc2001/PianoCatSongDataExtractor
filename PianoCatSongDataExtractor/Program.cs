using System.Text;

namespace PianoCatSongDataExtractor
{
    internal class Program
    {
        static Encoding gbk = Encoding.GetEncoding(936);
        static byte[] key;

        private static byte[] GetKey(byte[] fileData)
        {
            byte[] keyA = fileData.Skip(1024).Take(32).ToArray();
            byte[] keyB = fileData.Skip(1056).Take(32).ToArray();
            byte[] ret = new byte[32];
            for (int i = 0; i < 32; ++i)
                ret[i] = (byte)(keyB[i] - keyA[i]);
            return ret;
        }

        private static (string dirName, int dirDataLength, int dirDataOffset)[] GetAllDirs(byte[] fileData)
        {
            List<(string dirName, int dirDataLength, int dirOffset)> ret = new List<(string dirName, int dirDataLength, int dirOffset)>();
            int dirNum = BitConverter.ToInt32(fileData.Skip(252).Take(4).ToArray(), 0);
            for (int i = 0; i < dirNum; ++i)
            {
                int offset = 1280 + i * 128;
                byte[] item = fileData.Skip(offset).Take(128).ToArray();
                for (int j = 0; j < item.Length; ++j)
                    item[j] -= key[j % 32];
                string dirName = gbk.GetString(item.Take(120).ToArray()).TrimEnd('\0');
                int dirDataLength = BitConverter.ToInt32(item.Skip(120).Take(4).ToArray(), 0) << 7;
                int dirDataOffset = BitConverter.ToInt32(item.Skip(124).Take(4).ToArray(), 0);
                ret.Add((dirName, dirDataLength, dirDataOffset));
            }
            return ret.ToArray();
        }

        private static (string fileName, int songDataOffset, int songDataLength)[] GetAllFiles(byte[] fileData, int dirDataOffset, int dirDataLength)
        {
            List<(string fileName, int songDataOffset, int songDataLength)> ret = new List<(string fileName, int songDataOffset, int songDataLength)>();
            for (int i = 0; i < dirDataLength / 128; ++i)
            {
                int offset = dirDataOffset + i * 128;
                byte[] item = fileData.Skip(offset).Take(128).ToArray();
                for (int j = 0; j < item.Length; ++j)
                    item[j] -= key[j % 32];
                string fileName = gbk.GetString(item.Take(64).ToArray()).TrimEnd('\0');
                int songDataOffset = BitConverter.ToInt32(item.Skip(64).Take(4).ToArray(), 0);
                int songDataLength = BitConverter.ToInt32(item.Skip(68).Take(4).ToArray(), 0);
                ret.Add((fileName, songDataOffset, songDataLength));
            }
            return ret.ToArray();
        }

        private static byte[] GetSongData(byte[] fileData, int songDataOffset, int songDataLength)
        {
            return fileData.Skip(songDataOffset).Take(songDataLength).ToArray();
        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length != 2)
            {
                Console.WriteLine("调用说明: PianoCatSongDataExtractor.exe <input> <output>");
                Environment.Exit(1);
            }

            FileInfo input = new FileInfo(args[0]);
            if (!input.Exists)
            {
                Console.WriteLine($"找不到输入文件: {input.FullName}");
                Environment.Exit(1);
            }
            DirectoryInfo output = new DirectoryInfo(args[1]);
            if (!output.Exists)
            {
                Directory.CreateDirectory(output.FullName);
            }

            byte[] data = File.ReadAllBytes(input.FullName);
            key = GetKey(data);
            var dirs = GetAllDirs(data);
            foreach (var dir in dirs)
            {
                Directory.CreateDirectory(Path.Combine(output.FullName, dir.dirName));
                var songs = GetAllFiles(data, dir.dirDataOffset, dir.dirDataLength);
                foreach (var song in songs)
                {
                    Console.WriteLine($"{dir.dirName} -> {song.fileName}");
                    var songData = GetSongData(data, song.songDataOffset, song.songDataLength);
                    File.WriteAllBytes(Path.Combine(output.FullName, dir.dirName, song.fileName + ".mid"), songData);
                }
            }
        }
    }
}
