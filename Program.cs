using System;
using System.IO;
using System.Threading.Tasks;
using WTelegram;
using TL;
using CommandLine;
using System.Linq;

namespace TelegramConsoleClient
{
  // Классы для парсинга аргументов командной строки
  public class Options
  {
    [Option('a', "apiId", Required = true, HelpText = "Telegram API ID")]
    public int ApiId { get; set; }

    [Option('h', "apiHash", Required = true, HelpText = "Telegram API Hash")]
    public string ApiHash { get; set; }

    [Option('p', "phone", Required = true, HelpText = "Phone number in international format")]
    public string PhoneNumber { get; set; }

    [Option('c', "code", Required = false, HelpText = "Authorization code (if already received)")]
    public string Code { get; set; }

    [Option('s', "sessionFile", Required = false, Default = "telegram_session.dat", HelpText = "Path to session file")]
    public string SessionFilePath { get; set; }

    [Option('t', "target", Required = true, HelpText = "Target username or phone number")]
    public string Target { get; set; }

    [Option('m', "message", Required = true, HelpText = "Message to send")]
    public string Message { get; set; }

    [Option('w', "password", Required = false, HelpText = "Two-factor authentication password (if enabled)")]
    public string Password { get; set; }
  }

  // Коды возврата программы
  public enum ExitCode
  {
    Success = 0,
    AuthorizationRequired = 1,
    CodeRequired = 2,
    PasswordRequired = 3,
    MessageSendingFailed = 4,
    ConnectionFailed = 5,
    InvalidTarget = 6,
    UnknownError = 7
  }

  class Program
  {
    private static Options _options;
    private static Client _client;

    static async Task<int> Main(string[] args)
    {
      return await Parser.Default.ParseArguments<Options>(args)
          .MapResult(async (Options opts) =>
          {
            _options = opts;
            return await RunClientAsync();
          },
          errs => Task.FromResult((int)ExitCode.UnknownError));
    }

    private static async Task<int> RunClientAsync()
    {
      try
      {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Функция для предоставления конфигурации клиенту
        string Config(string what)
        {
          switch (what)
          {
            case "api_id": return _options.ApiId.ToString();
            case "api_hash": return _options.ApiHash;
            case "phone_number": return _options.PhoneNumber;
            case "verification_code": return _options.Code;
            case "password": return _options.Password;
            case "session_pathname": return _options.SessionFilePath;
            default: return null;
          }
        }

        // Инициализация клиента с помощью конфигурационной функции
        _client = new Client(Config);

        // Подписка на обновления (опционально)
        _client.OnOwnUpdates += (updates) => {
          Console.WriteLine($"Получено обновление: {updates.GetType().Name}");
          return Task.CompletedTask;
        };

        // Проверка авторизации
        var user = await _client.LoginUserIfNeeded();

        if (user == null)
        {
          if (string.IsNullOrEmpty(_options.Code))
          {
            Console.Error.WriteLine("Требуется код авторизации. Запустите приложение снова с параметром --code");
            return (int)ExitCode.CodeRequired;
          }
          if (string.IsNullOrEmpty(_options.Password))
          {
            Console.Error.WriteLine("Требуется пароль двухфакторной аутентификации. Запустите приложение снова с параметром --password");
            return (int)ExitCode.PasswordRequired;
          }
          return (int)ExitCode.AuthorizationRequired;
        }

        // Отправка сообщения
        var result = await SendMessageAsync(_options.Target, _options.Message);
        return result;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Ошибка: {ex.Message}");
        if (ex.InnerException != null)
          Console.Error.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
        return (int)ExitCode.UnknownError;
      }
      finally
      {
        _client?.Dispose();
      }
    }

    private static async Task<int> SendMessageAsync(string target, string message)
    {
      try
      {
        // Получение информации о целевом пользователе/канале
        InputPeer peer = null;

        // Проверяем, является ли цель пользователем (по имени пользователя)
        if (target.StartsWith("@"))
        {
          var resolved = await _client.Contacts_ResolveUsername(target.Substring(1));
          if (resolved?.User != null)
          {
            // Используем конструктор InputPeerUser с правильными параметрами
            var user = resolved.User;
            peer = new InputPeerUser(user.id, user.access_hash);
          }
          else if (resolved?.Chat != null)
          {
            // Используем конструктор InputPeerChat с правильными параметрами
            // Примечание: мы используем свойство id для доступа к id чата
            var chat = resolved.Chat;
            // Получаем доступ к id чата через приведение к User, Chat или Channel
            if (chat is Chat basicChat)
            {
              peer = new InputPeerChat(basicChat.id);
            }
          }
          else if (resolved?.Channel != null)
          {
            // Используем конструктор InputPeerChannel с правильными параметрами
            var channel = resolved.Channel;
            peer = new InputPeerChannel(channel.id, channel.access_hash);
          }
        }
        // Проверяем, является ли цель пользователем (по номеру телефона)
        else if (target.StartsWith("+"))
        {
          var contacts = await _client.Contacts_GetContacts();
          // Получаем пользователей из контактов
          var users = contacts.users.Values.ToArray();
          var user = users.FirstOrDefault(u => u.phone == target);

          if (user != null)
          {
            peer = new InputPeerUser(user.id, user.access_hash);
          }
        }

        if (peer == null)
        {
          Console.Error.WriteLine($"Не удалось найти пользователя или канал с идентификатором {target}");
          return (int)ExitCode.InvalidTarget;
        }

        // Отправка сообщения
        var sentMessage = await _client.SendMessageAsync(peer, message);
        Console.WriteLine($"Сообщение успешно отправлено. ID: {sentMessage.id}");
        return (int)ExitCode.Success;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
        return (int)ExitCode.MessageSendingFailed;
      }
    }
  }
}