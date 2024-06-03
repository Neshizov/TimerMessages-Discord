using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Timers;
using System.Linq;

class Program
{
    static string token;
    static List<ChannelConfig> channels = new List<ChannelConfig>();

    class ChannelConfig
    {
        public string ServerId { get; set; }
        public string ChannelId { get; set; }
        public string MessageText { get; set; }
        public List<string> ImageFiles { get; set; } = new List<string>();
        public int Interval { get; set; }
    }

    static async Task Main(string[] args)
    {
        try
        {
            InitializeConfiguration();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", token);

                var response = await client.GetAsync("https://discord.com/api/v9/users/@me");

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseContent);
                    string username = json["username"]?.ToString();

                    Console.WriteLine($"Имя аккаунта: {username}");

                    foreach (var channel in channels)
                    {
                        Timer timer = new Timer(channel.Interval * 1000);
                        timer.Elapsed += async (sender, e) => await SendMessage(client, channel);
                        timer.Start();

                        Console.WriteLine($"Настроен канал {channel.ServerId}/{channel.ChannelId} с интервалом {channel.Interval} секунд.");
                    }

                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine($"Не удалось войти в аккаунт. Код ошибки: {response.StatusCode}");
                    Console.ReadLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            Console.ReadLine();
        }
    }

    static void InitializeConfiguration()
    {
        token = GetTokenFromFile("Config/TOKEN.txt");
        if (string.IsNullOrEmpty(token))
            throw new Exception("Не удалось прочитать токен из файла.");

        ReadChannelsConfig("Config/CHANNELS.txt");
        if (channels.Count == 0)
            throw new Exception("Не удалось прочитать конфигурацию каналов.");
    }

    static string GetTokenFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл с токеном не найден.");

        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            if (line.StartsWith("TOKEN:", StringComparison.OrdinalIgnoreCase))
                return line.Substring("TOKEN:".Length).Trim();
        }

        throw new Exception("Токен не найден в файле.");
    }

    static void ReadChannelsConfig(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл с конфигурацией каналов не найден.");

        var lines = File.ReadAllLines(filePath);
        ChannelConfig currentChannel = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("CHANNEL:", StringComparison.OrdinalIgnoreCase))
            {
                if (currentChannel != null)
                {
                    channels.Add(currentChannel);
                }
                currentChannel = new ChannelConfig();
                var url = line.Substring("CHANNEL:".Length).Trim();
                var parts = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 5 && parts[2] == "channels" && parts[3].All(char.IsDigit) && parts[4].All(char.IsDigit))
                {
                    currentChannel.ServerId = parts[3].Trim();
                    currentChannel.ChannelId = parts[4].Trim();
                }
                else
                {
                    Console.WriteLine($"Неверный формат строки CHANNEL: {url}");
                    throw new FormatException("Неверный формат строки CHANNEL.");
                }
            }
            else if (line.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase) && currentChannel != null)
            {
                currentChannel.MessageText = line.Substring("TEXT:".Length).Trim();
            }
            else if (line.StartsWith("IMAGE:", StringComparison.OrdinalIgnoreCase) && currentChannel != null)
            {
                var images = line.Substring("IMAGE:".Length).Trim().Split(',');
                foreach (var image in images)
                {
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        currentChannel.ImageFiles.Add(image.Trim());
                    }
                }
            }
            else if (line.StartsWith("TIME:", StringComparison.OrdinalIgnoreCase) && currentChannel != null)
            {
                if (int.TryParse(line.Substring("TIME:".Length).Trim(), out int time))
                {
                    currentChannel.Interval = time;
                }
            }
        }

        if (currentChannel != null)
        {
            channels.Add(currentChannel);
        }
    }

    static async Task SendMessage(HttpClient client, ChannelConfig channel)
    {
        try
        {
            var content = new MultipartFormDataContent();

            if (!string.IsNullOrEmpty(channel.MessageText) && channel.MessageText != "None")
                content.Add(new StringContent(channel.MessageText), "content");

            foreach (var imageFile in channel.ImageFiles)
            {
                string imagePath = Path.Combine("Config", imageFile);
                if (File.Exists(imagePath))
                {
                    byte[] imageData = File.ReadAllBytes(imagePath);
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                    content.Add(imageContent, "file", imageFile);
                }
                else
                {
                    Console.WriteLine($"Файл изображения не найден: {imageFile}");
                }
            }

            Console.WriteLine($"Отправка сообщения в канал {channel.ChannelId} на сервере {channel.ServerId}");
            var response = await client.PostAsync($"https://discord.com/api/v9/channels/{channel.ChannelId}/messages", content);
            if (response.IsSuccessStatusCode)
            {
                DateTime now = DateTime.Now;
                Console.WriteLine($"Сообщение отправлено в {now:HH:mm}. Следующее отправится через {channel.Interval} секунд.");
            }
            else
            {
                Console.WriteLine($"Не удалось отправить сообщение. Код ошибки: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Ответ от сервера: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
        }
    }
}
