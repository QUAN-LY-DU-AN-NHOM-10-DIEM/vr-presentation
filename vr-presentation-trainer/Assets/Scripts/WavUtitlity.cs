using System.IO;
using System.Text;
using UnityEngine;

public static class WavUtility
{
    public static string Save(string fileName, AudioClip clip)
    {
        // Đảm bảo tên file có đuôi .wav
        if (!fileName.ToLower().EndsWith(".wav")) fileName += ".wav";

        // Tạo đường dẫn lưu file an toàn trên cả PC và kính Quest
        string filepath = Path.Combine(Application.persistentDataPath, fileName);

        // Chuyển đổi dữ liệu AudioClip thành file WAV
        using (var fileStream = new FileStream(filepath, FileMode.Create))
        using (var writer = new BinaryWriter(fileStream))
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // Viết Header chuẩn của file WAV
            writer.Write(Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.UTF8.GetBytes("data"));
            writer.Write(samples.Length * 2);

            // Chuyển đổi âm thanh thô (float) sang định dạng âm thanh 16-bit
            int sampleInt;
            for (int i = 0; i < samples.Length; i++)
            {
                sampleInt = (int)(samples[i] * 32767f);
                if (sampleInt > 32767) sampleInt = 32767;
                else if (sampleInt < -32768) sampleInt = -32768;
                writer.Write((short)sampleInt);
            }
        }

        return filepath; // Trả về đường dẫn để ta biết file đã lưu ở đâu
    }
}