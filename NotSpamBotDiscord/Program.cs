using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Timers;

class Program
{
    static string token;
    static string serverId;
    static string channelId;
    static string messageText;
    static List<string> imageFiles = new List<string>();
    static int interval;

    static async Task Main(string[] args)
    {
        token = GetTokenFromFile("Config/TOKEN.txt");
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Не удалось прочитать токен из файла.");
            Console.ReadLine();
            return;
        }

        string serverLink = GetServerLinkFromFile("Config/SERVER.txt");
        if (string.IsNullOrEmpty(serverLink) || !ParseServerLink(serverLink))
        {
            Console.WriteLine("Ссылка отсутствует или неверного формата. Добавьте ссылку на чат в конфиг, в файл SERVER.txt");
            Console.ReadLine();
            return;
        }

        ReadMessageConfig("Config/MESS.txt");
        if (interval <= 0)
        {
            Console.WriteLine("Не удалось прочитать сообщение или интервал времени из файла.");
            Console.ReadLine();
            return;
        }

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
                Console.WriteLine($"Ссылка на сервер: {serverLink}");

                Timer timer = new Timer(interval * 1000);
                timer.Elapsed += async (sender, e) => await SendMessage(client);
                timer.Start();

                Console.ReadLine();
            }
            else
            {
                Console.WriteLine($"Не удалось войти в аккаунт. Код ошибки: {response.StatusCode}");
                Console.ReadLine();
            }
        }
    }

    static string GetTokenFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Файл с токеном не найден.");
            Console.ReadLine();
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("TOKEN:", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring("TOKEN:".Length).Trim();
                }
            }
            Console.WriteLine("Токен не найден в файле.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении файла: {ex.Message}");
            Console.ReadLine();
        }
        return null;
    }

    static string GetServerLinkFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Файл с ссылкой на сервер не найден.");
            Console.ReadLine();
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (Uri.IsWellFormedUriString(line, UriKind.Absolute))
                {
                    return line.Trim();
                }
            }
            Console.WriteLine("Ссылка на сервер не найдена в файле.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении файла: {ex.Message}");
            Console.ReadLine();
        }
        return null;
    }

    static void ReadMessageConfig(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Файл с сообщением не найден.");
            Console.ReadLine();
            return;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase))
                {
                    messageText = line.Substring("TEXT:".Length).Trim();
                }
                else if (line.StartsWith("IMAGE:", StringComparison.OrdinalIgnoreCase))
                {
                    var images = line.Substring("IMAGE:".Length).Trim().Split(',');
                    foreach (var image in images)
                    {
                        imageFiles.Add(image.Trim());
                    }
                }
                else if (line.StartsWith("TIME:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring("TIME:".Length).Trim(), out int time))
                    {
                        interval = time;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении файла: {ex.Message}");
            Console.ReadLine();
        }
    }

    static bool ParseServerLink(string serverLink)
    {
        try
        {
            var uri = new Uri(serverLink);
            var segments = uri.AbsolutePath.Split('/');

            if (segments.Length >= 3 && segments[1] == "channels")
            {
                serverId = segments[2];
                channelId = segments[3];
                return true;
            }
            else
            {
                Console.WriteLine("Неверный формат ссылки.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при разборе ссылки на сервер: {ex.Message}");
        }
        return false;
    }

    static async Task SendMessage(HttpClient client)
    {
        try
        {
            var content = new MultipartFormDataContent();

            if (messageText != "None")
            {
                content.Add(new StringContent(messageText), "content");
            }

            foreach (var imageFile in imageFiles)
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

            Console.WriteLine($"Отправка сообщения в канал {channelId} на сервере {serverId}");
            var response = await client.PostAsync($"https://discord.com/api/v9/channels/{channelId}/messages", content);
            if (response.IsSuccessStatusCode)
            {
                DateTime now = DateTime.Now;
                Console.WriteLine($"Сообщение отправлено в {now:HH:mm}. Следующее отправится через {interval} секунд.");
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
